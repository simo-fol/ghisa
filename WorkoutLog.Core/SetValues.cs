using System.Globalization;
using System.Text.RegularExpressions;

namespace WorkoutLog.Core;

/// <summary>Helpers for the free text the log book uses: "15-20-22.5", "12,5-5", "3 X 7/9", "REST 60\"".</summary>
public static partial class SetValues
{
    /// <summary>Splits a cell such as "15-20-22,5" or "12 15 17.5" into one value per set.</summary>
    public static List<string> Split(string text)
    {
        var normalized = DecimalComma().Replace(text, "$1.$2");
        // "20 (facili) -12.5" uses dashes; "12 15 17.5" only spaces.
        var separator = normalized.Contains('-') ? DashSeparator() : SpaceSeparator();
        return separator.Split(normalized)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();
    }

    /// <summary>Joins per-set values the way they are written in the sheet: "15-20-22.5".</summary>
    public static string Join(IEnumerable<string> values) =>
        string.Join("-", values.Select(v => v.Trim().Replace(',', '.')).Where(v => v.Length > 0));

    /// <summary>Per-set reps target when the goal is a plain list such as "10-8-6-6" or "15"; empty otherwise.</summary>
    public static List<string> TargetReps(string goal) =>
        RepsList().IsMatch(goal.Trim()) ? Split(goal) : [];

    /// <summary>Number of sets from the "Sets" column ("3", "3-4", "4").</summary>
    public static int SetCount(string sets, int fallback = 3)
    {
        var numbers = Digits().Matches(sets).Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture))
            .Where(n => n is > 0 and <= 10).ToList();
        return numbers.Count > 0 ? numbers.Max() : fallback;
    }

    /// <summary>Rest time read from a coach note such as "REST 60\"" or "REST BREVI (60\")".</summary>
    public static int? RestSeconds(string note)
    {
        var m = RestPattern().Match(note);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>Adds a step to the leading number of a value ("22.5" + 2.5 = "25"), keeping any suffix.</summary>
    public static string Bump(string value, double step)
    {
        var m = LeadingNumber().Match(value.Replace(',', '.'));
        if (!m.Success)
            return step > 0 ? Format(step) : "";
        var current = double.Parse(m.Value, CultureInfo.InvariantCulture);
        var next = Math.Max(0, current + step);
        return Format(next) + value[m.Length..];
    }

    public static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(\d),(\d)")]
    private static partial Regex DecimalComma();

    [GeneratedRegex(@"\s*-+\s*")]
    private static partial Regex DashSeparator();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceSeparator();

    [GeneratedRegex(@"^\d+(\s*-\s*\d+)*$")]
    private static partial Regex RepsList();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"REST[^\d]*(\d{2,3})", RegexOptions.IgnoreCase)]
    private static partial Regex RestPattern();

    [GeneratedRegex(@"^\s*\d+(\.\d+)?")]
    private static partial Regex LeadingNumber();
}
