namespace HdRezka.IntegrationTests;

public sealed class CompactCatalogTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task CompactCatalogEndpoints_LoadSliderAndQuickContent()
    {
        var email = Environment.GetEnvironmentVariable("HDREZKA_TEST_EMAIL");
        var password = Environment.GetEnvironmentVariable("HDREZKA_TEST_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var origin = Environment.GetEnvironmentVariable("HDREZKA_TEST_ORIGIN")
            ?? "https://hdrezka.fi";
        using var client = new Client(origin);
        await client.LoginAsync(email, password);
        try
        {
            var items = await client.Catalog.GetNewestSliderAsync();
            var item = items.First(candidate => candidate.Id.HasValue);
            var quickContent = await client.Catalog.GetQuickContentAsync(item);

            Assert.Equal(item.Id, quickContent.Id);
            Assert.False(string.IsNullOrWhiteSpace(quickContent.Title));
            Assert.True(quickContent.Url.IsAbsoluteUri);
        }
        finally
        {
            await client.LogoutAsync();
        }
    }
}
