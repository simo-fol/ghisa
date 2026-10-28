namespace WorkoutLog.Core;

/// <summary>Previous result of an exercise, with where it was found.</summary>
public sealed record PastResult(string Where, string Load, string Reps);

public static class SessionPlanner
{
    /// <summary>All slots of a split in chronological order (microcycle by microcycle, week by week).</summary>
    public static IEnumerable<Slot> Slots(Protocol p, string letter)
    {
        for (var m = 0; m < p.Microcycles.Count; m++)
        {
            var block = p.Microcycles[m].Split(letter);
            if (block is null) continue;
            for (var w = 0; w < block.WeekLabels.Count; w++) yield return new Slot(m, w);
        }
    }

    public static SplitBlock? Block(Protocol p, string letter, Slot slot) =>
        slot.Micro < p.Microcycles.Count ? p.Microcycles[slot.Micro].Split(letter) : null;

    public static bool IsFilled(Protocol p, string letter, Slot slot) =>
        Block(p, letter, slot)?.IsWeekFilled(slot.Week) == true;

    /// <summary>The slot right after the last one that has data: the workout to do next.</summary>
    public static Slot NextSlot(Protocol p, string letter)
    {
        var slots = Slots(p, letter).ToList();
        if (slots.Count == 0) return default;
        var last = slots.FindLastIndex(s => IsFilled(p, letter, s));
        return slots[Math.Min(last + 1, slots.Count - 1)];
    }

    public static Slot? Step(Protocol p, string letter, Slot from, int delta)
    {
        var slots = Slots(p, letter).ToList();
        var i = slots.IndexOf(from) + delta;
        return i >= 0 && i < slots.Count ? slots[i] : null;
    }

    public static string Label(Protocol p, Slot slot) =>
        $"Microciclo {p.Microcycles[slot.Micro].Number} · Settimana {slot.WeekLabel}";

    /// <summary>
    /// Most recent result of an exercise before the given slot: first in the same split of this
    /// protocol, then anywhere in earlier protocols with the same exercise name.
    /// </summary>
    public static PastResult? LastResult(IReadOnlyList<Protocol> protocols, int protocolIndex, string letter,
        Slot before, ExerciseRow exercise)
    {
        var p = protocols[protocolIndex];
        var slots = Slots(p, letter).ToList();
        var start = slots.IndexOf(before);
        var position = Block(p, letter, before)?.Exercises.IndexOf(exercise) ?? -1;
        for (var i = (start < 0 ? slots.Count : start) - 1; i >= 0; i--)
        {
            var block = Block(p, letter, slots[i])!;
            var match = block.Exercises.FirstOrDefault(e => SameName(e.Name, exercise.Name))
                        ?? (position >= 0 && position < block.Exercises.Count ? block.Exercises[position] : null);
            var cell = match?.Weeks[slots[i].Week];
            if (cell is { IsEmpty: false }) return new PastResult(Label(p, slots[i]), cell.Load, cell.Reps);
        }

        for (var pi = protocolIndex - 1; pi >= 0; pi--)
        {
            var old = protocols[pi];
            for (var m = old.Microcycles.Count - 1; m >= 0; m--)
            {
                foreach (var block in old.Microcycles[m].Splits)
                {
                    var match = block.Exercises.FirstOrDefault(e => SameName(e.Name, exercise.Name));
                    if (match is null) continue;
                    for (var w = match.Weeks.Length - 1; w >= 0; w--)
                    {
                        if (!match.Weeks[w].IsEmpty)
                            return new PastResult($"{old.SheetName} · {Label(old, new Slot(m, w))}", match.Weeks[w].Load, match.Weeks[w].Reps);
                    }
                }
            }
        }
        return null;
    }

    public static bool SameName(string a, string b) => Normalize(a) == Normalize(b);

    static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
