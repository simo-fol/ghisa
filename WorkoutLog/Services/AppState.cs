using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkoutLog.Services;

public sealed class AppState
{
    /// <summary>File name of the working copy in <see cref="IPlatformServices.DocumentsDirectory"/>.</summary>
    public string? WorkbookFile { get; set; }

    /// <summary>Bookmark of the file originally picked, to write results back to it.</summary>
    public string? SourceBookmark { get; set; }

    public string? SelectedSheet { get; set; }

    /// <summary>Personal notes per exercise name.</summary>
    public Dictionary<string, string> Notes { get; set; } = [];

    /// <summary>Rest seconds chosen per exercise name.</summary>
    public Dictionary<string, int> Rest { get; set; } = [];

    public WorkoutDraft? Draft { get; set; }
}

/// <summary>A workout in progress, kept so nothing is lost if iOS closes the app.</summary>
public sealed class WorkoutDraft
{
    public string Sheet { get; set; } = "";
    public string Split { get; set; } = "";
    public int Micro { get; set; }
    public int Week { get; set; }
    public DateTime StartedAt { get; set; }
    public List<DraftExercise> Exercises { get; set; } = [];
}

public sealed class DraftExercise
{
    public int Row { get; set; }
    public List<DraftSet> Sets { get; set; } = [];
}

public sealed class DraftSet
{
    public string Kg { get; set; } = "";
    public string Reps { get; set; } = "";
    public bool Done { get; set; }
}

[JsonSerializable(typeof(AppState))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppStateJson : JsonSerializerContext;

public sealed class StateStore(IPlatformServices platform)
{
    string FilePath => Path.Combine(platform.StateDirectory, "state.json");

    public AppState Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), AppStateJson.Default.AppState) ?? new();
        }
        catch (Exception)
        {
            // A corrupt state file must not stop the app from starting.
        }
        return new();
    }

    public void Save(AppState state)
    {
        Directory.CreateDirectory(platform.StateDirectory);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, AppStateJson.Default.AppState));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
