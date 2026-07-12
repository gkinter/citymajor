namespace Forge.SimCore;

/// <summary>
/// File paths to game JSON packs under <c>base/data/</c>.
/// Used by Unity and desktop hosts; WASM uses embedded resources instead.
/// </summary>
public sealed class SimDataPaths
{
    public string EventsJsonPath { get; init; } = "";
    public string TechnologiesJsonPath { get; init; } = "";
    public string LawsJsonPath { get; init; } = "";

    /// <summary>Resolve standard data paths from a repository or content root.</summary>
    public static SimDataPaths FromContentRoot(string contentRoot) => new()
    {
        EventsJsonPath = Path.Combine(contentRoot, "base", "data", "events", "events.json"),
        TechnologiesJsonPath = Path.Combine(contentRoot, "base", "data", "tech", "technologies.json"),
        LawsJsonPath = Path.Combine(contentRoot, "base", "data", "laws", "laws.json"),
    };
}
