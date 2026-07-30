namespace HdRezka.Translators;

internal static class TranslatorSelector
{
    public static IReadOnlyList<Translator> Sort(
        IEnumerable<Translator> translators,
        IEnumerable<int> preferred,
        IEnumerable<int> nonPreferred)
    {
        var source = translators.ToList();
        var preferredIds = preferred.ToList();
        var nonPreferredIds = nonPreferred.ToList();
        var rank = new Dictionary<int, int>();

        for (var index = 0; index < preferredIds.Count; index++)
        {
            rank[preferredIds[index]] = index;
        }

        var neutralRank = preferredIds.Count;
        for (var index = 0; index < nonPreferredIds.Count; index++)
        {
            rank.TryAdd(nonPreferredIds[index], neutralRank + index + 1);
        }

        return source
            .Select((translator, index) => (translator, index))
            .OrderBy(item => rank.GetValueOrDefault(item.translator.Id, neutralRank))
            .ThenBy(item => item.index)
            .Select(item => item.translator)
            .ToList();
    }
}
