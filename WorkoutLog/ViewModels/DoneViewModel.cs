using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace WorkoutLog.ViewModels;

public partial class DoneViewModel(MainViewModel main, string title, string slotLabel, int recorded, int total,
    int minutes, bool savedToOriginal) : ViewModelBase
{
    public string Title => $"{title} completato";
    public string SlotLabel => slotLabel;
    public string Summary => $"{recorded} di {total} esercizi registrati · {minutes} min";
    public string SaveInfo => savedToOriginal
        ? "Risultati scritti nel tuo file Excel."
        : "Risultati scritti nella copia dell'app (File › Sul mio iPhone › Allenamento). Usa Condividi per mandarla dove vuoi.";

    [RelayCommand]
    Task Share() => main.ShareWorkbookAsync();

    [RelayCommand]
    void Home() => main.ShowHome();
}
