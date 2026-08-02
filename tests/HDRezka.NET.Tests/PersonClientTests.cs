namespace HdRezka.Tests;

public sealed class PersonClientTests
{
    [Fact]
    public async Task GetAsync_ParsesBiographyAndGroupedFilmography()
    {
        using var httpClient = new HttpClient(new StubHttpHandler((request, _) =>
        {
            Assert.Equal("/person/6046-ben-test/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(StubHttpHandler.Html(PersonHtml));
        }));
        using var client = new Client("https://hdrezka.test", httpClient: httpClient);

        var person = await client.People.GetAsync("/person/6046-ben-test/");

        Assert.Equal(6046, person.Id);
        Assert.Equal("Бен Тест", person.Name);
        Assert.Equal("Ben Test", person.OriginalName);
        Assert.Equal(1972, person.BirthYear);
        Assert.Null(person.BirthDate);
        Assert.Equal(54, person.Age);
        Assert.Equal("Лондон, Великобритания", person.BirthPlace);
        Assert.Equal(["режиссер", "сценарист"], person.Professions);
        var career = Assert.Single(person.Careers);
        Assert.Equal("Режиссер", career.Name);
        Assert.Equal("В базе 1 фильм", career.Summary);
        var media = Assert.Single(career.Items);
        Assert.Equal(7.42, media.Rating);
        Assert.Equal([2025], media.Years);
        Assert.Equal(["США"], media.Countries);
        Assert.Equal(["Драмы"], media.Genres);
        Assert.True(media.HasTrailer);
    }

    private const string PersonHtml = """
        <html><head><title>Person</title></head><body>
          <div class="b-post b-person">
            <h1><span class="t1" itemprop="name">Бен Тест</span>
              <span class="t2" itemprop="alternativeHeadline">Ben Test</span></h1>
            <div class="b-sidecover"><img itemprop="image" src="/person.jpg"></div>
            <table class="b-post__info">
              <tr><td class="l">Карьера:</td><td>
                <span itemprop="jobTitle">режиссер</span>,
                <span itemprop="jobTitle">сценарист</span>
              </td></tr>
              <tr><td class="l">Дата рождения:</td><td>
                <time itemprop="birthDate" datetime="1972-00-00">1972</time> (54 года)
              </td></tr>
              <tr><td class="l">Место рождения:</td><td>Лондон, Великобритания</td></tr>
            </table>
            <div class="b-person__career">
              <h2>Режиссер</h2><span class="b-person__career_stats">В базе 1 фильм</span>
              <div class="b-content__inline_item" data-id="10">
                <div class="b-content__inline_item-cover">
                  <a href="/films/drama/10-test.html"><img src="/cover.jpg"></a>
                  <span class="cat films"><i class="b-category-bestrating">(7.42)</i></span>
                  <i class="show-trailer"></i>
                </div>
                <div class="b-content__inline_item-link">
                  <a href="/films/drama/10-test.html">Фильм</a>
                  <div>2025, США, Драмы</div>
                </div>
              </div>
            </div>
          </div>
        </body></html>
        """;
}
