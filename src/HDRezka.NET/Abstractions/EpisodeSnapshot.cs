namespace HdRezka.Abstractions;

internal sealed record EpisodeSnapshot(
    IReadOnlyDictionary<int, string> Seasons,
    IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> Episodes,
    int? SelectedSeason,
    int? SelectedEpisode);
