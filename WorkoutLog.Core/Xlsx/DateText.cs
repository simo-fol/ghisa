using System.Globalization;
using System.Text;

namespace WorkoutLog.Core.Xlsx;

/// <summary>
/// Excel turns entries like "7/9" (reps range) or "10-8-8" (reps) into dates.
/// This renders those dates back into the text that was originally typed.
/// </summary>
public static class DateText
{
    public static string Format(double serial, string formatCode)
    {
        var date = new DateTime(1899, 12, 30).AddDays(Math.Floor(serial));
        // A year close to "now" was added by Excel itself: it was not typed.
        var typedYear = date.Year is < 2020 or > 2040;

        var sb = new StringBuilder();
        var code = formatCode.Split(';')[0];
        for (var i = 0; i < code.Length;)
        {
            var ch = code[i];
            var run = 1;
            while (i + run < code.Length && code[i + run] == ch) run++;
            switch (ch)
            {
                case 'd':
                    sb.Append(date.Day.ToString(CultureInfo.InvariantCulture));
                    break;
                case 'm':
                    sb.Append(date.Month.ToString(CultureInfo.InvariantCulture));
                    break;
                case 'y':
                    if (typedYear)
                        sb.Append((date.Year % 100).ToString(CultureInfo.InvariantCulture));
                    else
                        TrimTrailingSeparator(sb);
                    break;
                default:
                    sb.Append(code, i, run);
                    break;
            }
            i += run;
        }
        return sb.ToString().Trim();
    }

    static void TrimTrailingSeparator(StringBuilder sb)
    {
        while (sb.Length > 0 && !char.IsDigit(sb[^1])) sb.Length--;
    }
}
