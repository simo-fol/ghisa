# Allenamento

App per iPhone (Avalonia, .NET 10) che legge la scheda Excel del coach, ti guida nello split del giorno e scrive carichi e ripetizioni nel file.

- Scegli lo split (A/B/C/D): l'app trova da sola microciclo e settimana da fare (la prima colonna SET vuota dopo l'ultima compilata). Con ‹ › puoi spostarti su un'altra settimana.
- Per ogni esercizio vedi note del coach, rep goal, serie e **l'ultima volta** (stesso split, oppure lo stesso esercizio in un protocollo precedente).
- Kg e reps partono dall'ultima volta; li correggi con − / + o scrivendoli. Il ✓ chiude la serie e avvia il **recupero** (letto da `REST 60"` nelle note, altrimenti 1'30"; tocca il pulsante per cambiarlo, viene ricordato per esercizio).
- Notifica a fine recupero anche a telefono bloccato; schermo sempre acceso durante l'allenamento.
- **Le tue note** per esercizio restano salvate e le ritrovi la volta dopo.
- **Termina e salva** scrive nel foglio, nelle celle Carico/Reps della settimana, nel formato `20-22.5-25` / `8-7-6`. Formattazione, colori e celle unite restano intatti.
- Se l'app viene chiusa a metà allenamento, lo riprendi dalla home.

## Struttura

| Progetto | Cosa contiene |
|---|---|
| `WorkoutLog.Core` | Lettura/scrittura xlsx senza dipendenze, interpretazione della scheda, calcolo della prossima seduta. Nessuna UI: è la parte da riportare tale e quale in un'eventuale versione Swift. |
| `WorkoutLog` | Interfaccia Avalonia (MVVM) condivisa. |
| `WorkoutLog.Desktop` | Versione Windows/Mac per provare l'app al computer. |
| `WorkoutLog.iOS` | App iPhone: notifiche del timer, schermo acceso, condivisione del file. |

Provarla sul PC:

```bash
dotnet run --project WorkoutLog.Desktop
```

## Build dell'app iPhone

Ogni push su `main` avvia `.github/workflows/ios.yml` su un runner macOS gratuito di GitHub: produce `Allenamento.ipa` non firmato e lo pubblica nella release **latest** del repository (oltre che come artifact della run).

## Installazione sull'iPhone

L'ipa non è firmato: lo firma SideStore (o AltStore) con il tuo Apple ID gratuito. Con un Apple ID gratuito l'app va rinnovata ogni 7 giorni (in SideStore: *My Apps › Refresh*).

1. Configura SideStore seguendo la guida ufficiale su https://sidestore.io (serve un computer solo la prima volta).
2. Dall'iPhone apri la release *latest* del repository e scarica `Allenamento.ipa`.
3. In SideStore: **My Apps › +** e scegli il file scaricato.
4. Apri *Allenamento*, consenti le notifiche e carica il file Excel dall'app File (iCloud Drive, Sul mio iPhone…).

Dove finisce l'Excel compilato: l'app lavora su una copia visibile in **File › Sul mio iPhone › Allenamento** e, quando iOS lo permette, aggiorna anche il file originale che hai scelto. Dalla home (⇪) o a fine allenamento puoi condividerlo (Mail, WhatsApp, iCloud…).

Il file Excel non va mai nel repository (`*.xlsx` è in `.gitignore`).
