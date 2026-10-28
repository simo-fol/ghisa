using WorkoutLog.Core.Xlsx;

namespace WorkoutLog.Core;

/// <summary>The loaded workbook: parsed protocols plus the package used to write results back.</summary>
public sealed class LogBook
{
    readonly XlsxPackage _package;

    LogBook(XlsxPackage package)
    {
        _package = package;
        Protocols = WorkbookParser.Parse(package);
    }

    public IReadOnlyList<Protocol> Protocols { get; }

    public static LogBook Load(byte[] xlsx) => new(XlsxPackage.Load(xlsx));

    /// <summary>Writes the load/reps of one exercise into the given week of the sheet.</summary>
    public void Record(Protocol protocol, ExerciseRow exercise, int week, string load, string reps)
    {
        var sheet = _package.Sheets.First(s => s.Name == protocol.SheetName);
        var cell = exercise.Weeks[week];
        sheet.SetText(cell.LoadRef, load);
        sheet.SetText(cell.RepsRef, reps);
        cell.Load = load;
        cell.Reps = reps;
    }

    public byte[] Save() => _package.Save();
}
