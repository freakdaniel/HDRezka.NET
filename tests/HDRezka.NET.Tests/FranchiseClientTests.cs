namespace HdRezka.Tests;

public sealed class FranchiseClientTests
{
    [Fact]
    public async Task GetPageAndFranchiseAsync_ParseOrderedParts()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            var html = request.RequestUri!.AbsolutePath == "/franchises/page/2/"
                ? DirectoryHtml
                : FranchiseHtml;
            return Task.FromResult(StubHttpHandler.Html(html));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var directory = await client.Franchises.GetPageAsync(2);
        var summary = Assert.Single(directory.Items);
        Assert.Equal(42, summary.Id);
        Assert.Equal(3, summary.PartCount);
        Assert.Equal(9, directory.TotalPages);

        var franchise = await client.Franchises.GetAsync(summary);

        Assert.Equal(summary.ImageUrl, franchise.ImageUrl);
        Assert.Equal([1, 2], franchise.Parts.Select(part => part.Order));
        Assert.Equal(100, franchise.Parts[0].MediaId);
        Assert.Equal(2001, franchise.Parts[0].Year);
        Assert.Equal(7.5, franchise.Parts[0].Rating);
    }

    private const string DirectoryHtml = """
        <html><body>
          <div class="b-content__collections_item">
            <img class="cover" src="/franchise.jpg">
            <div class="num">3</div>
            <a class="title" href="/franchises/42-test-franchise/">Test franchise</a>
          </div>
          <div class="b-navigation"><a>9</a></div>
        </body></html>
        """;

    private const string FranchiseHtml = """
        <html><body><h1>Test franchise</h1>
          <div class="b-post__partcontent_item">
            <div class="td num">2</div><div class="td title"><a href="/films/101-second.html">Second</a></div>
            <div class="td year">2002 год</div><div class="td rating">7.20</div>
          </div>
          <div class="b-post__partcontent_item">
            <div class="td num">1</div><div class="td title"><a href="/films/100-first.html">First</a></div>
            <div class="td year">2001 год</div><div class="td rating">7.50</div>
          </div>
        </body></html>
        """;
}
