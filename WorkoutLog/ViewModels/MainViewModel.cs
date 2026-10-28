using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WorkoutLog.Core;
using WorkoutLog.Services;

namespace WorkoutLog.ViewModels;

/// <summary>Owns the loaded log book and app state, and switches between pages.</summary>
public partial class MainViewModel : ViewModelBase
{
    readonly StateStore _store;
    DispatcherTimer? _toastTimer;

    public MainViewModel() : this(new DesktopPlatformServices()) { }

    public MainViewModel(IPlatformServices platform)
    {
        Platform = platform;
        _store = new StateStore(platform);
        State = _store.Load();
        TryOpenWorkingCopy();

        if (Book is not null && State.Draft is { } draft && FindProtocol(draft.Sheet) is { } pi)
            CurrentPage = new WorkoutViewModel(this, pi, draft.Split, new Slot(draft.Micro, draft.Week), draft);
        else
            CurrentPage = new HomeViewModel(this);
    }

    public IPlatformServices Platform { get; }
    public AppState State { get; }
    public LogBook? Book { get; private set; }

    /// <summary>Set by the view: gives access to the file pickers.</summary>
    public Func<IStorageProvider?> StorageProvider { get; set; } = () => null;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    [ObservableProperty]
    public partial string? Toast { get; set; }

    public string? WorkbookPath =>
        State.WorkbookFile is { } f ? Path.Combine(Platform.DocumentsDirectory, f) : null;

    void TryOpenWorkingCopy()
    {
        try
        {
            if (WorkbookPath is { } path && File.Exists(path))
                Book = LogBook.Load(File.ReadAllBytes(path));
        }
        catch (Exception)
        {
            Book = null;
        }
    }

    public int? FindProtocol(string sheet)
    {
        var i = Book?.Protocols.ToList().FindIndex(p => p.SheetName == sheet) ?? -1;
        return i >= 0 ? i : null;
    }

    public void PersistState() => _store.Save(State);

    public void ShowHome() => CurrentPage = new HomeViewModel(this);

    public void StartWorkout(int protocolIndex, string split, Slot slot)
    {
        State.Draft = null;
        CurrentPage = new WorkoutViewModel(this, protocolIndex, split, slot, null);
    }

    public async Task ImportAsync()
    {
        var provider = StorageProvider();
        if (provider is null) return;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Scegli la scheda Excel",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Excel")
                {
                    Patterns = ["*.xlsx"],
                    AppleUniformTypeIdentifiers = ["org.openxmlformats.spreadsheetml.sheet"],
                    MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
                },
            ],
        });
        if (files.Count == 0) return;
        var file = files[0];

        byte[] bytes;
        await using (var s = await file.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        LogBook book;
        try
        {
            book = LogBook.Load(bytes);
        }
        catch (Exception)
        {
            ShowToast("Questo file non sembra una scheda valida");
            return;
        }
        if (book.Protocols.Count == 0)
        {
            ShowToast("Nessun microciclo trovato nel file");
            return;
        }

        Directory.CreateDirectory(Platform.DocumentsDirectory);
        State.WorkbookFile = file.Name;
        await File.WriteAllBytesAsync(WorkbookPath!, bytes);
        State.SourceBookmark = file.CanBookmark ? await file.SaveBookmarkAsync() : null;
        State.SelectedSheet = book.Protocols[^1].SheetName;
        State.Draft = null;
        PersistState();
        Book = book;
        ShowHome();
        ShowToast($"Scheda caricata: {book.Protocols.Count} fogli");
    }

    /// <summary>
    /// Saves the log book to the working copy and, when possible, back to the file that was picked.
    /// Returns true if the original file was updated too.
    /// </summary>
    public async Task<bool> SaveWorkbookAsync()
    {
        if (Book is null || WorkbookPath is null) return false;
        var bytes = Book.Save();
        await File.WriteAllBytesAsync(WorkbookPath, bytes);

        if (State.SourceBookmark is null || StorageProvider() is not { } provider) return false;
        try
        {
            var original = await provider.OpenFileBookmarkAsync(State.SourceBookmark);
            if (original is null) return false;
            await using var s = await original.OpenWriteAsync();
            if (s.CanSeek) s.SetLength(0);
            await s.WriteAsync(bytes);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task ShareWorkbookAsync()
    {
        if (WorkbookPath is null || !File.Exists(WorkbookPath)) return;
        if (await Platform.ShareFileAsync(WorkbookPath)) return;

        // Desktop: let the user pick where to save a copy.
        if (StorageProvider() is not { } provider) return;
        var target = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = State.WorkbookFile,
            DefaultExtension = "xlsx",
        });
        if (target is null) return;
        await using var s = await target.OpenWriteAsync();
        if (s.CanSeek) s.SetLength(0);
        await s.WriteAsync(await File.ReadAllBytesAsync(WorkbookPath));
        ShowToast("Copia salvata");
    }

    public void ShowToast(string message)
    {
        Toast = message;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (_, _) =>
        {
            Toast = null;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }
}
