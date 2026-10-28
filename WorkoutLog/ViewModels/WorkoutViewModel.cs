using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutLog.Core;
using WorkoutLog.Services;

namespace WorkoutLog.ViewModels;

public partial class WorkoutViewModel : ViewModelBase
{
    readonly MainViewModel _main;
    readonly Protocol _protocol;
    readonly WorkoutDraft _draft;

    public WorkoutViewModel(MainViewModel main, int protocolIndex, string split, Slot slot, WorkoutDraft? draft)
    {
        _main = main;
        _protocol = main.Book!.Protocols[protocolIndex];
        Split = split;
        Slot = slot;
        Timer = new RestTimerViewModel(main.Platform);

        var block = SessionPlanner.Block(_protocol, split, slot)!;
        _draft = draft ?? new WorkoutDraft
        {
            Sheet = _protocol.SheetName,
            Split = split,
            Micro = slot.Micro,
            Week = slot.Week,
            StartedAt = DateTime.Now,
        };
        foreach (var row in block.Exercises)
        {
            var last = SessionPlanner.LastResult(main.Book.Protocols, protocolIndex, split, slot, row);
            var saved = _draft.Exercises.FirstOrDefault(e => e.Row == row.Row);
            Exercises.Add(new ExerciseViewModel(this, row, slot.Week, last, saved));
        }
        if (Exercises.FirstOrDefault(e => !e.IsComplete) is { } first) first.IsExpanded = true;

        main.Platform.KeepScreenOn(true);
        _ = main.Platform.RequestNotificationPermissionAsync();
        SaveDraft();
    }

    internal MainViewModel Main => _main;

    public string Split { get; }
    public Slot Slot { get; }
    public string Title => $"Split {Split}";
    public string SlotLabel => SessionPlanner.Label(_protocol, Slot);
    public ObservableCollection<ExerciseViewModel> Exercises { get; } = [];
    public RestTimerViewModel Timer { get; }

    public int CompletedCount => Exercises.Count(e => e.IsComplete);
    public string ProgressText => $"{CompletedCount}/{Exercises.Count}";
    public double Progress => Exercises.Count == 0 ? 0 : (double)CompletedCount / Exercises.Count;

    internal void OnSetChanged(ExerciseViewModel exercise, SetViewModel set, bool justCompleted)
    {
        if (justCompleted)
        {
            _main.Platform.Haptic();
            if (exercise.IsComplete)
            {
                exercise.IsExpanded = false;
                var next = Exercises.SkipWhile(e => e != exercise).Skip(1).FirstOrDefault(e => !e.IsComplete)
                           ?? Exercises.FirstOrDefault(e => !e.IsComplete);
                if (next is not null) next.IsExpanded = true;
            }
            Timer.Start(exercise.RestSeconds, exercise.Name);
        }
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(Progress));
        SaveDraft();
    }

    internal void Expand(ExerciseViewModel target)
    {
        var open = !target.IsExpanded;
        foreach (var e in Exercises) e.IsExpanded = false;
        target.IsExpanded = open;
    }

    internal void SaveDraft()
    {
        _draft.Exercises = Exercises.Select(e => e.ToDraft()).ToList();
        _main.State.Draft = _draft;
        _main.PersistState();
    }

    [RelayCommand]
    void Back()
    {
        SaveDraft();
        Timer.Stop();
        _main.Platform.KeepScreenOn(false);
        _main.ShowHome();
    }

    [RelayCommand]
    async Task Finish()
    {
        var recorded = 0;
        foreach (var e in Exercises)
        {
            var done = e.Sets.Where(s => s.Done).ToList();
            if (done.Count == 0) continue;
            _main.Book!.Record(_protocol, e.Row, Slot.Week,
                SetValues.Join(done.Select(s => s.Kg)),
                SetValues.Join(done.Select(s => s.Reps)));
            recorded++;
        }
        if (recorded == 0)
        {
            _main.ShowToast("Segna almeno una serie come fatta");
            return;
        }

        var original = await _main.SaveWorkbookAsync();
        Timer.Stop();
        _main.Platform.KeepScreenOn(false);
        _main.State.Draft = null;
        _main.PersistState();
        var minutes = (int)Math.Round((DateTime.Now - _draft.StartedAt).TotalMinutes);
        _main.CurrentPage = new DoneViewModel(_main, Title, SlotLabel, recorded, Exercises.Count, minutes, original);
    }
}
