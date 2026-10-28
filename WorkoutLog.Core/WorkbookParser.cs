using System.Globalization;
using WorkoutLog.Core.Xlsx;

namespace WorkoutLog.Core;

/// <summary>
/// Reads the coach's log book layout: "MICROCICLO n" blocks, each with
/// "SPLIT X" columns side by side (Esercizio, Note, Rep Goal, Sets, then Carico/Reps per week).
/// </summary>
public static class WorkbookParser
{
    public static List<Protocol> Parse(XlsxPackage pkg) =>
        pkg.Sheets.Select(ParseSheet).Where(p => p.Microcycles.Count > 0).ToList();

    static Protocol ParseSheet(XlsxSheet sheet)
    {
        var cells = sheet.Cells().ToDictionary(c => (c.Row, c.Col));
        string Text(int row, int col) => cells.TryGetValue((row, col), out var c) ? c.Text : "";
        string? Below(string label) => cells.Values
            .Where(c => c.Text.Equals(label, StringComparison.OrdinalIgnoreCase))
            .Select(c => Text(c.Row + 1, c.Col)).FirstOrDefault(t => t.Length > 0);

        var protocol = new Protocol
        {
            SheetName = sheet.Name,
            StartLabel = Below("INIZIO PROTOCOLLO"),
            Coach = Below("PERSONAL COACH"),
        };

        var microRows = cells.Values
            .Where(c => c.Text.StartsWith("MICROCICLO", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Row).ToList();
        var maxRow = cells.Count == 0 ? 0 : cells.Keys.Max(k => k.Row);

        for (var i = 0; i < microRows.Count; i++)
        {
            var start = microRows[i].Row;
            var end = i + 1 < microRows.Count ? microRows[i + 1].Row : maxRow + 1;
            var number = int.TryParse(new string(microRows[i].Text.Where(char.IsDigit).ToArray()),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : i + 1;
            var micro = new Microcycle { Number = number };

            var splitCells = cells.Values
                .Where(c => c.Row > start && c.Row < end && c.Text.StartsWith("SPLIT", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Col).ToList();
            if (splitCells.Count == 0) continue;
            var splitRow = splitCells[0].Row;
            var headerRow = splitRow + 1;
            var lastCol = cells.Keys.Where(k => k.Row == headerRow).Select(k => k.Col).DefaultIfEmpty(0).Max();

            for (var s = 0; s < splitCells.Count; s++)
            {
                var from = splitCells[s].Col;
                var to = s + 1 < splitCells.Count ? splitCells[s + 1].Col - 1 : lastCol;
                var block = ParseBlock(splitCells[s].Text, from, to, splitRow, headerRow, end, Text);
                if (block is not null) micro.Splits.Add(block);
            }
            protocol.Microcycles.Add(micro);
        }
        return protocol;
    }

    static SplitBlock? ParseBlock(string title, int from, int to, int splitRow, int headerRow, int end,
        Func<int, int, string> text)
    {
        int? nameCol = null, noteCol = null, goalCol = null, setsCol = null;
        var loads = new List<int>();
        for (var col = from; col <= to; col++)
        {
            switch (text(headerRow, col).ToUpperInvariant())
            {
                case "ESERCIZIO": nameCol = col; break;
                case "NOTE": noteCol = col; break;
                case "REP GOAL": goalCol = col; break;
                case "SETS": setsCol = col; break;
                case "CARICO": loads.Add(col); break;
            }
        }
        if (nameCol is null || loads.Count == 0) return null;

        var letter = title[5..].Replace("/", "").Trim();
        var block = new SplitBlock
        {
            Letter = letter.Length > 0 ? letter : title,
            WeekLabels = loads.Select((c, i) => text(splitRow, c) is { Length: > 0 } l ? l : $"SET {Slot.Roman[i]}").ToList(),
        };
        for (var row = headerRow + 1; row < end; row++)
        {
            var name = text(row, nameCol.Value);
            if (name.Length == 0) continue;
            block.Exercises.Add(new ExerciseRow
            {
                Row = row,
                Name = name,
                Note = noteCol is { } nc ? text(row, nc) : "",
                RepGoal = goalCol is { } gc ? text(row, gc) : "",
                Sets = setsCol is { } sc ? text(row, sc) : "",
                Weeks = loads.Select(c => new WeekCell
                {
                    LoadRef = CellRef.Build(row, c),
                    RepsRef = CellRef.Build(row, c + 1),
                    Load = text(row, c),
                    Reps = text(row, c + 1),
                }).ToArray(),
            });
        }
        return block;
    }
}
