using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using UIKit;
using UserNotifications;
using WorkoutLog.Services;

namespace WorkoutLog.iOS;

public sealed class IosPlatformServices : IPlatformServices
{
    const string RestId = "rest-timer";

    public string DocumentsDirectory { get; } = Folder(NSSearchPathDirectory.DocumentDirectory);
    public string StateDirectory { get; } = Folder(NSSearchPathDirectory.ApplicationSupportDirectory);

    static string Folder(NSSearchPathDirectory kind)
    {
        var path = NSFileManager.DefaultManager.GetUrls(kind, NSSearchPathDomain.User)[0].Path!;
        Directory.CreateDirectory(path);
        return path;
    }

    public void KeepScreenOn(bool on) => UIApplication.SharedApplication.IdleTimerDisabled = on;

    public async Task RequestNotificationPermissionAsync()
    {
        var center = UNUserNotificationCenter.Current;
        center.Delegate ??= new ForegroundPresenter();
        await center.RequestAuthorizationAsync(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound);
    }

    public void ScheduleRestAlert(TimeSpan after, string exercise)
    {
        var content = new UNMutableNotificationContent
        {
            Title = "Recupero finito",
            Body = $"{exercise}: vai con la prossima serie 💪",
            Sound = UNNotificationSound.Default,
        };
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(Math.Max(1, after.TotalSeconds), false);
        var center = UNUserNotificationCenter.Current;
        center.RemovePendingNotificationRequests([RestId]);
        center.AddNotificationRequest(UNNotificationRequest.FromIdentifier(RestId, content, trigger), null);
    }

    public void CancelRestAlert()
    {
        var center = UNUserNotificationCenter.Current;
        center.RemovePendingNotificationRequests([RestId]);
        center.RemoveDeliveredNotifications([RestId]);
    }

    public void Haptic() => new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium).ImpactOccurred();

    public Task<bool> ShareFileAsync(string path)
    {
        var root = TopController();
        if (root?.View is not { } view) return Task.FromResult(false);
        var sheet = new UIActivityViewController([NSUrl.FromFilename(path)], null);
        if (sheet.PopoverPresentationController is { } popover)
        {
            popover.SourceView = view;
            popover.SourceRect = new CGRect(view.Bounds.GetMidX(), view.Bounds.GetMidY(), 0, 0);
        }
        root.PresentViewController(sheet, true, null);
        return Task.FromResult(true);
    }

    static UIViewController? TopController()
    {
        var window = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(s => s.Windows)
            .FirstOrDefault(w => w.IsKeyWindow);
        var vc = window?.RootViewController;
        while (vc?.PresentedViewController is { } presented) vc = presented;
        return vc;
    }

    /// <summary>Shows the "rest is over" banner and sound even while the app is open.</summary>
    sealed class ForegroundPresenter : UNUserNotificationCenterDelegate
    {
        public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler) =>
            completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List |
                              UNNotificationPresentationOptions.Sound);
    }
}
