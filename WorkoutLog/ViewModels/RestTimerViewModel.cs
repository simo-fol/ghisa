using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutLog.Services;

namespace WorkoutLog.ViewModels;

/// <summary>
/// Rest countdown. It works on the end time, not by counting ticks, so it stays right
/// even when iOS suspends the app; the scheduled notification rings meanwhile.
/// </summary>
public partial class RestTimerViewModel(IPlatformServices platform) : ViewModelBase
{
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    DateTime _endsAt;
    int _total;
    string _exercise = "";
    bool _subscribed;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsFinished { get; set; }

    [ObservableProperty]
    public partial string Remaining { get; set; } = "0:00";

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string Caption { get; set; } = "";

    public void Start(int seconds, string exercise)
    {
        if (!_subscribed)
        {
            _tick.Tick += (_, _) => Update();
            _subscribed = true;
        }
        _exercise = exercise;
        _total = seconds;
        _endsAt = DateTime.UtcNow.AddSeconds(seconds);
        Caption = $"Recupero · {exercise}";
        IsFinished = false;
        IsRunning = true;
        platform.ScheduleRestAlert(TimeSpan.FromSeconds(seconds), exercise);
        _tick.Start();
        Update();
    }

    void Update()
    {
        var left = _endsAt - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            left = TimeSpan.Zero;
            if (!IsFinished)
            {
                IsFinished = true;
                Caption = "Recupero finito: vai!";
                platform.Haptic();
            }
        }
        var secs = (int)Math.Ceiling(left.TotalSeconds);
        Remaining = $"{secs / 60}:{secs % 60:00}";
        Progress = _total == 0 ? 0 : 1 - left.TotalSeconds / _total;
    }

    [RelayCommand]
    void Add15() => Shift(15);

    [RelayCommand]
    void Remove15() => Shift(-15);

    void Shift(int seconds)
    {
        if (!IsRunning) return;
        var now = DateTime.UtcNow;
        if (_endsAt < now) _endsAt = now;
        _endsAt = _endsAt.AddSeconds(seconds);
        _total = Math.Max(_total + seconds, 1);
        var left = _endsAt - now;
        if (left > TimeSpan.Zero)
        {
            IsFinished = false;
            Caption = $"Recupero · {_exercise}";
            platform.ScheduleRestAlert(left, _exercise);
        }
        Update();
    }

    [RelayCommand]
    public void Stop()
    {
        _tick.Stop();
        IsRunning = false;
        IsFinished = false;
        platform.CancelRestAlert();
    }
}
