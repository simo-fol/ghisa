using System;
using System.IO;
using System.Threading.Tasks;

namespace WorkoutLog.Services;

/// <summary>Things each platform does its own way (iOS notifications, share sheet, ...).</summary>
public interface IPlatformServices
{
    /// <summary>Folder for the working copy of the Excel file (visible in the iOS Files app).</summary>
    string DocumentsDirectory { get; }

    /// <summary>Folder for the app's own state.</summary>
    string StateDirectory { get; }

    void KeepScreenOn(bool on);

    Task RequestNotificationPermissionAsync();

    /// <summary>Schedules the "rest is over" alert, replacing any previous one.</summary>
    void ScheduleRestAlert(TimeSpan after, string exercise);

    void CancelRestAlert();

    /// <summary>Short vibration/feedback, if the device supports it.</summary>
    void Haptic();

    /// <summary>Opens the system share sheet for a file. Returns false if not supported.</summary>
    Task<bool> ShareFileAsync(string path);
}

/// <summary>Desktop fallback: no notifications or share sheet.</summary>
public sealed class DesktopPlatformServices : IPlatformServices
{
    static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkoutLog");

    public string DocumentsDirectory => Root;
    public string StateDirectory => Root;
    public void KeepScreenOn(bool on) { }
    public Task RequestNotificationPermissionAsync() => Task.CompletedTask;
    public void ScheduleRestAlert(TimeSpan after, string exercise) { }
    public void CancelRestAlert() { }
    public void Haptic() { }
    public Task<bool> ShareFileAsync(string path) => Task.FromResult(false);
}
