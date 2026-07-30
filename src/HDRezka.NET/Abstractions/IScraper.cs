namespace HdRezka.Abstractions;

internal interface IScraper
{
    Task<PageSnapshot> ParseMediaPageAsync(
        string html,
        Uri url,
        CancellationToken cancellationToken);

    EpisodeSnapshot ParseEpisodes(string seasonsHtml, string episodesHtml);

    MediaStream ParseStream(
        StreamSnapshot snapshot,
        int? season,
        int? episode,
        string name,
        int translatorId);

    Task<IReadOnlyList<FastSearchResult>> ParseFastSearchAsync(
        string html,
        Uri origin,
        CancellationToken cancellationToken);

    Task<PageResult<SearchResult>> ParseSearchPageAsync(
        string html,
        Uri origin,
        int page,
        CancellationToken cancellationToken);
}
