using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WorkoutLog.Core.Xlsx;

/// <summary>
/// Minimal .xlsx reader/writer. Reads cell values and rewrites only the values of
/// the cells we touch, so formatting, merged cells and images stay intact.
/// </summary>
public sealed class XlsxPackage
{
    internal static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    readonly List<(string Name, byte[] Data)> _entries = [];
    readonly List<string> _sharedStrings = [];
    readonly List<int> _xfNumFmt = [];
    readonly Dictionary<int, string> _numFmts = [];

    public IReadOnlyList<XlsxSheet> Sheets { get; private set; } = [];

    public static XlsxPackage Load(byte[] data)
    {
        var pkg = new XlsxPackage();
        using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
        {
            foreach (var e in zip.Entries)
            {
                using var s = e.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                pkg._entries.Add((e.FullName, ms.ToArray()));
            }
        }
        pkg.ReadSharedStrings();
        pkg.ReadStyles();
        pkg.ReadSheets();
        return pkg;
    }

    public byte[] Save()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in _entries)
            {
                var sheet = Sheets.FirstOrDefault(s => s.Path == name && s.IsDirty);
                var bytes = sheet is null ? data : Serialize(sheet.Document);
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var es = entry.Open();
                es.Write(bytes);
            }
        }
        foreach (var s in Sheets) s.IsDirty = false;
        return ms.ToArray();
    }

    static byte[] Serialize(XDocument doc)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
        using (var w = XmlWriter.Create(ms, settings)) doc.Save(w);
        return ms.ToArray();
    }

    byte[]? Entry(string name) => _entries.FirstOrDefault(e => e.Name == name).Data;

    static XDocument Parse(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return XDocument.Load(ms, LoadOptions.PreserveWhitespace);
    }

    void ReadSharedStrings()
    {
        var data = Entry("xl/sharedStrings.xml");
        if (data is null) return;
        foreach (var si in Parse(data).Root!.Elements(S + "si"))
            _sharedStrings.Add(string.Concat(si.Descendants(S + "t").Select(t => t.Value)));
    }

    void ReadStyles()
    {
        var data = Entry("xl/styles.xml");
        if (data is null) return;
        var root = Parse(data).Root!;
        foreach (var f in root.Element(S + "numFmts")?.Elements(S + "numFmt") ?? [])
            _numFmts[(int)f.Attribute("numFmtId")!] = (string)f.Attribute("formatCode")!;
        foreach (var xf in root.Element(S + "cellXfs")?.Elements(S + "xf") ?? [])
            _xfNumFmt.Add((int?)xf.Attribute("numFmtId") ?? 0);
    }

    void ReadSheets()
    {
        var wb = Parse(Entry("xl/workbook.xml")!).Root!;
        var rels = Parse(Entry("xl/_rels/workbook.xml.rels")!).Root!
            .Elements(PkgRel + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);
        var sheets = new List<XlsxSheet>();
        foreach (var s in wb.Element(S + "sheets")!.Elements(S + "sheet"))
        {
            var target = rels[(string)s.Attribute(Rel + "id")!];
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            var data = Entry(path);
            if (data is null) continue;
            sheets.Add(new XlsxSheet(this, (string)s.Attribute("name")!, path, Parse(data)));
        }
        Sheets = sheets;
    }

    internal string CellText(XElement c)
    {
        var type = (string?)c.Attribute("t");
        var v = c.Element(S + "v")?.Value;
        switch (type)
        {
            case "s":
                return int.TryParse(v, out var i) && i < _sharedStrings.Count ? _sharedStrings[i] : "";
            case "inlineStr":
                return string.Concat(c.Element(S + "is")?.Descendants(S + "t").Select(t => t.Value) ?? []);
            case "str":
            case "e":
                return v ?? "";
            case "b":
                return v == "1" ? "TRUE" : "FALSE";
        }
        if (string.IsNullOrEmpty(v)) return "";
        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return v;
        var style = (int?)c.Attribute("s") ?? 0;
        var fmtId = style < _xfNumFmt.Count ? _xfNumFmt[style] : 0;
        var code = DateFormatCode(fmtId);
        return code is null ? number.ToString("0.###", CultureInfo.InvariantCulture) : DateText.Format(number, code);
    }

    string? DateFormatCode(int fmtId)
    {
        if (fmtId is >= 14 and <= 22) return "d/m/yyyy";
        if (!_numFmts.TryGetValue(fmtId, out var code)) return null;
        var plain = new string(code.Where(ch => ch != '\\' && ch != '"').ToArray()).ToLowerInvariant();
        return plain.Contains('d') || plain.Contains('y') ? plain : null;
    }
}

public sealed class XlsxSheet
{
    readonly XlsxPackage _pkg;
    readonly XElement _sheetData;

    internal XlsxSheet(XlsxPackage pkg, string name, string path, XDocument doc)
    {
        _pkg = pkg;
        Name = name;
        Path = path;
        Document = doc;
        _sheetData = doc.Root!.Element(XlsxPackage.S + "sheetData")!;
    }

    public string Name { get; }
    public string Path { get; }
    internal XDocument Document { get; }
    public bool IsDirty { get; internal set; }

    /// <summary>All non-empty cells as (row, column, reference, text).</summary>
    public IEnumerable<XlsxCell> Cells()
    {
        foreach (var c in _sheetData.Elements(XlsxPackage.S + "row").Elements(XlsxPackage.S + "c"))
        {
            var r = (string?)c.Attribute("r");
            if (r is null) continue;
            var text = _pkg.CellText(c).Trim();
            if (text.Length == 0) continue;
            var (row, col) = CellRef.Parse(r);
            yield return new XlsxCell(row, col, r, text);
        }
    }

    /// <summary>Writes a text value into a cell, keeping its style.</summary>
    public void SetText(string reference, string text)
    {
        var (rowIdx, colIdx) = CellRef.Parse(reference);
        var row = _sheetData.Elements(XlsxPackage.S + "row").FirstOrDefault(r => (int?)r.Attribute("r") == rowIdx);
        if (row is null)
        {
            row = new XElement(XlsxPackage.S + "row", new XAttribute("r", rowIdx));
            var after = _sheetData.Elements(XlsxPackage.S + "row").LastOrDefault(r => (int)r.Attribute("r")! < rowIdx);
            if (after is null) _sheetData.AddFirst(row); else after.AddAfterSelf(row);
        }
        var cell = row.Elements(XlsxPackage.S + "c").FirstOrDefault(c => (string?)c.Attribute("r") == reference);
        if (cell is null)
        {
            cell = new XElement(XlsxPackage.S + "c", new XAttribute("r", reference));
            var after = row.Elements(XlsxPackage.S + "c").LastOrDefault(c => CellRef.Parse((string)c.Attribute("r")!).Col < colIdx);
            if (after is null) row.AddFirst(cell); else after.AddAfterSelf(cell);
        }
        cell.Attribute("t")?.Remove();
        cell.RemoveNodes();
        if (!string.IsNullOrEmpty(text))
        {
            cell.SetAttributeValue("t", "inlineStr");
            cell.Add(new XElement(XlsxPackage.S + "is",
                new XElement(XlsxPackage.S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        }
        IsDirty = true;
    }
}

public readonly record struct XlsxCell(int Row, int Col, string Ref, string Text);

public static class CellRef
{
    public static (int Row, int Col) Parse(string reference)
    {
        int col = 0, i = 0;
        for (; i < reference.Length && char.IsLetter(reference[i]); i++)
            col = col * 26 + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
        return (int.Parse(reference.AsSpan(i), CultureInfo.InvariantCulture), col);
    }

    public static string Build(int row, int col)
    {
        var letters = "";
        for (; col > 0; col = (col - 1) / 26)
            letters = (char)('A' + (col - 1) % 26) + letters;
        return letters + row.ToString(CultureInfo.InvariantCulture);
    }
}
