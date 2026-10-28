using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutLog.Core;

namespace WorkoutLog.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    readonly MainViewModel _main;

    public HomeViewModel(MainViewModel main)
    {
        _main = main;
        if (main.Book is { } book)
        {
            Protocols = book.Protocols.Select(p => p.SheetName).ToList();
            var selected = main.State.SelectedSheet is { } s ? main.FindProtocol(s) : null;
            SelectedProtocol = selected ?? Protocols.Count - 1;
        }
    }

    public bool HasBook => _main.Book is not null;
    public System.Collections.Generic.List<string> Protocols { get; } = [];
    public bool HasManyProtocols => Protocols.Count > 1;
    public string FileName => _main.State.WorkbookFile ?? "";

    public ObservableCollection<SplitCardViewModel> Splits { get; } = [];

    [ObservableProperty]
    public partial int SelectedProtocol { get; set; } = -1;

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Subtitle { get; set; } = "";

    public bool HasDraft => _main.State.Draft is not null;

    public string DraftLabel => _main.State.Draft is { } d && _main.FindProtocol(d.Sheet) is { } pi
        ? $"Split {d.Split} · {SessionPlanner.Label(_main.Book!.Protocols[pi], new Slot(d.Micro, d.Week))}"
        : "";

    partial void OnSelectedProtocolChanged(int value)
    {
        Splits.Clear();
        if (_main.Book is null || value < 0 || value >= _main.Book.Protocols.Count) return;
        var p = _main.Book.Protocols[value];
        _main.State.SelectedSheet = p.SheetName;
        _main.PersistState();
        Title = p.SheetName;
        Subtitle = string.Join(" · ", new[] { p.StartLabel == p.SheetName ? null : p.StartLabel, p.Coach is null ? null : "Coach " + p.Coach }
            .Where(s => !string.IsNullOrEmpty(s)));
        foreach (var letter in p.SplitLetters)
            Splits.Add(new SplitCardViewModel(_main, value, letter));
    }

    [RelayCommand]
    Task Import() => _main.ImportAsync();

    [RelayCommand]
    Task Share() => _main.ShareWorkbookAsync();

    [RelayCommand]
    void ResumeDraft()
    {
        if (_main.State.Draft is { } d && _main.FindProtocol(d.Sheet) is { } pi)
            _main.CurrentPage = new WorkoutViewModel(_main, pi, d.Split, new Slot(d.Micro, d.Week), d);
    }

    [RelayCommand]
    void DiscardDraft()
    {
        _main.State.Draft = null;
        _main.PersistState();
        OnPropertyChanged(nameof(HasDraft));
    }
}

public partial class SplitCardViewModel : ViewModelBase
{
    readonly MainViewModel _main;
    readonly int _protocolIndex;
    readonly Protocol _protocol;

    public SplitCardViewModel(MainViewModel main, int protocolIndex, string letter)
    {
        _main = main;
        _protocolIndex = protocolIndex;
        _protocol = main.Book!.Protocols[protocolIndex];
        Letter = letter;
        Slot = SessionPlanner.NextSlot(_protocol, letter);
    }

    public string Letter { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlotLabel), nameof(Preview), nameof(ExerciseCount), nameof(IsFilled), nameof(StatusLabel))]
    public partial Slot Slot { get; set; }

    SplitBlock? Block => SessionPlanner.Block(_protocol, Letter, Slot);

    public string SlotLabel => SessionPlanner.Label(_protocol, Slot);
    public string Preview => string.Join(" · ", Block?.Exercises.Take(3).Select(e => Pretty(e.Name)) ?? []);
    public int ExerciseCount => Block?.Exercises.Count ?? 0;
    public bool IsFilled => SessionPlanner.IsFilled(_protocol, Letter, Slot);
    public string StatusLabel => IsFilled ? "GIÀ REGISTRATA" : "PROSSIMA";

    [RelayCommand]
    void Previous()
    {
        if (SessionPlanner.Step(_protocol, Letter, Slot, -1) is { } s) Slot = s;
    }

    [RelayCommand]
    void Next()
    {
        if (SessionPlanner.Step(_protocol, Letter, Slot, +1) is { } s) Slot = s;
    }

    [RelayCommand]
    void Start() => _main.StartWorkout(_protocolIndex, Letter, Slot);

    internal static string Pretty(string name) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Trim().ToLowerInvariant());
}
