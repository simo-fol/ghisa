using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutLog.Core;
using WorkoutLog.Services;

namespace WorkoutLog.ViewModels;

public partial class ExerciseViewModel : ViewModelBase
{
    static readonly int[] RestChoices = [45, 60, 90, 120, 150, 180, 240];

    readonly WorkoutViewModel _workout;
    readonly string _key;
    readonly bool _ready;

    public ExerciseViewModel(WorkoutViewModel workout, ExerciseRow row, int week, PastResult? last, DraftExercise? saved)
    {
        _workout = workout;
        Row = row;
        _key = row.Name.Trim().ToUpperInvariant();
        Last = last;

        var state = workout.Main.State;
        RestSeconds = state.Rest.TryGetValue(_key, out var r) ? r : SetValues.RestSeconds(row.Note) ?? 90;
        UserNote = state.Notes.TryGetValue(_key, out var n) ? n : "";

        if (saved is not null)
        {
            foreach (var s in saved.Sets)
                Sets.Add(new SetViewModel(this, Sets.Count + 1, s.Kg, s.Reps, s.Done));
        }
        else
        {
            // Already recorded in this week: show it to edit. Otherwise start from last time.
            var current = row.Weeks[week];
            var kgSource = !current.IsEmpty ? current.Load : last?.Load ?? "";
            var repsSource = !current.IsEmpty ? current.Reps : last?.Reps ?? "";
            var kgs = SetValues.Split(kgSource);
            var reps = SetValues.Split(repsSource);
            if (reps.Count == 0) reps = SetValues.TargetReps(row.RepGoal);
            var count = SetValues.SetCount(row.Sets);
            if (!current.IsEmpty) count = System.Math.Max(kgs.Count, reps.Count);
            for (var i = 0; i < count; i++)
                Sets.Add(new SetViewModel(this, i + 1, Pick(kgs, i), Pick(reps, i), !current.IsEmpty));
        }
        _ready = true;
    }

    static string Pick(System.Collections.Generic.List<string> values, int i) =>
        values.Count == 0 ? "" : values[System.Math.Min(i, values.Count - 1)];

    public ExerciseRow Row { get; }
    public PastResult? Last { get; }

    public string Name => SplitCardViewModel.Pretty(Row.Name);
    public string CoachNote => Row.Note;
    public bool HasCoachNote => Row.Note.Length > 0;
    public string RepGoal => Row.RepGoal.Replace("\n", "  ·  ");
    public bool HasRepGoal => Row.RepGoal.Length > 0;
    public string SetsText => Row.Sets.Length > 0 ? $"{Row.Sets} serie" : $"{Sets.Count} serie";

    public bool HasLast => Last is not null;
    public string LastLoad => Last is null ? "" : FormatPair(Last.Load, Last.Reps);
    public string LastWhere => Last?.Where ?? "";

    static string FormatPair(string load, string reps) =>
        (load.Length > 0 ? $"{load} kg" : "") + (load.Length > 0 && reps.Length > 0 ? "  ×  " : "") + reps;

    public ObservableCollection<SetViewModel> Sets { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RestLabel))]
    public partial int RestSeconds { get; set; }

    public string RestLabel => RestSeconds % 60 == 0 ? $"{RestSeconds / 60}'" : $"{RestSeconds / 60}'{RestSeconds % 60:00}\"";

    [ObservableProperty]
    public partial string UserNote { get; set; }

    public int DoneCount => Sets.Count(s => s.Done);
    public bool IsComplete => Sets.Count > 0 && Sets.All(s => s.Done);
    public string ProgressText => $"{DoneCount}/{Sets.Count}";

    partial void OnUserNoteChanged(string value)
    {
        if (!_ready) return;
        var notes = _workout.Main.State.Notes;
        if (string.IsNullOrWhiteSpace(value)) notes.Remove(_key); else notes[_key] = value;
        _workout.Main.PersistState();
    }

    partial void OnRestSecondsChanged(int value)
    {
        if (!_ready) return;
        _workout.Main.State.Rest[_key] = value;
        _workout.Main.PersistState();
    }

    internal void OnSetChanged(SetViewModel set, bool justCompleted)
    {
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(ProgressText));
        _workout.OnSetChanged(this, set, justCompleted);
    }

    [RelayCommand]
    void Toggle() => _workout.Expand(this);

    [RelayCommand]
    void CycleRest()
    {
        var next = RestChoices.FirstOrDefault(c => c > RestSeconds);
        RestSeconds = next == 0 ? RestChoices[0] : next;
    }

    [RelayCommand]
    void AddSet()
    {
        var last = Sets.LastOrDefault();
        Sets.Add(new SetViewModel(this, Sets.Count + 1, last?.Kg ?? "", last?.Reps ?? "", false));
        OnPropertyChanged(nameof(SetsText));
        OnSetChanged(Sets[^1], false);
    }

    [RelayCommand]
    void RemoveSet()
    {
        if (Sets.Count <= 1) return;
        Sets.RemoveAt(Sets.Count - 1);
        OnPropertyChanged(nameof(SetsText));
        _workout.OnSetChanged(this, Sets[^1], false);
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(ProgressText));
    }

    internal DraftExercise ToDraft() => new()
    {
        Row = Row.Row,
        Sets = Sets.Select(s => new DraftSet { Kg = s.Kg, Reps = s.Reps, Done = s.Done }).ToList(),
    };
}

public partial class SetViewModel : ViewModelBase
{
    readonly ExerciseViewModel _exercise;

    public SetViewModel(ExerciseViewModel exercise, int number, string kg, string reps, bool done)
    {
        _exercise = exercise;
        Number = number;
        _kg = kg;
        _reps = reps;
        _done = done;
    }

    public int Number { get; }

    string _kg, _reps;
    bool _done;

    public string Kg
    {
        get => _kg;
        set { if (SetProperty(ref _kg, value)) _exercise.OnSetChanged(this, false); }
    }

    public string Reps
    {
        get => _reps;
        set { if (SetProperty(ref _reps, value)) _exercise.OnSetChanged(this, false); }
    }

    public bool Done
    {
        get => _done;
        set { if (SetProperty(ref _done, value)) _exercise.OnSetChanged(this, value); }
    }

    [RelayCommand] void KgUp() => Kg = SetValues.Bump(Kg, 2.5);
    [RelayCommand] void KgDown() => Kg = SetValues.Bump(Kg, -2.5);
    [RelayCommand] void RepsUp() => Reps = SetValues.Bump(Reps, 1);
    [RelayCommand] void RepsDown() => Reps = SetValues.Bump(Reps, -1);
    [RelayCommand] void ToggleDone() => Done = !Done;
}
