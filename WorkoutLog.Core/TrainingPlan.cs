namespace WorkoutLog.Core;

/// <summary>One sheet of the workbook: a training protocol lasting one or two months.</summary>
public sealed class Protocol
{
    public required string SheetName { get; init; }
    public string? StartLabel { get; init; }
    public string? Coach { get; init; }
    public List<Microcycle> Microcycles { get; } = [];

    public IEnumerable<string> SplitLetters =>
        Microcycles.SelectMany(m => m.Splits).Select(s => s.Letter).Distinct();
}

/// <summary>A block of the sheet ("MICROCICLO n") holding the 4 weeks of every split.</summary>
public sealed class Microcycle
{
    public required int Number { get; init; }
    public List<SplitBlock> Splits { get; } = [];

    public SplitBlock? Split(string letter) => Splits.FirstOrDefault(s => s.Letter == letter);
}

/// <summary>A training day (SPLIT A/B/C/D) inside a microcycle.</summary>
public sealed class SplitBlock
{
    public required string Letter { get; init; }
    public required IReadOnlyList<string> WeekLabels { get; init; }
    public List<ExerciseRow> Exercises { get; } = [];

    public bool IsWeekFilled(int week) => Exercises.Any(e => !e.Weeks[week].IsEmpty);
}

public sealed class ExerciseRow
{
    public required int Row { get; init; }
    public required string Name { get; init; }
    public string Note { get; init; } = "";
    public string RepGoal { get; init; } = "";
    public string Sets { get; init; } = "";
    public required WeekCell[] Weeks { get; init; }
}

/// <summary>Load and reps cells of one week ("SET I".."SET IV") of an exercise.</summary>
public sealed class WeekCell
{
    public required string LoadRef { get; init; }
    public required string RepsRef { get; init; }
    public string Load { get; set; } = "";
    public string Reps { get; set; } = "";
    public bool IsEmpty => Load.Length == 0 && Reps.Length == 0;
}

/// <summary>Position of a workout: microcycle index and week index (both 0-based).</summary>
public readonly record struct Slot(int Micro, int Week)
{
    public static readonly string[] Roman = ["I", "II", "III", "IV", "V", "VI"];
    public string WeekLabel => Week < Roman.Length ? Roman[Week] : (Week + 1).ToString();
}
