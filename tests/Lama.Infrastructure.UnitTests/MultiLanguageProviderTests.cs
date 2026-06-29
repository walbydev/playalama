using Lama.Contracts;
using Lama.Infrastructure.Lexicon;

namespace Lama.Infrastructure.UnitTests;

public sealed class MultiLanguageProviderTests
{
    private sealed class FakeProvider(string name, ISet<string> words) : IGameLanguageProvider
    {
        public IReadOnlySet<string> GetDictionary() => (IReadOnlySet<string>)words;
        public IReadOnlyDictionary<char, int> GetLetterScores() => new Dictionary<char, int> { ['A'] = 1 };
        public IReadOnlyDictionary<char, int> GetTileDistribution() => new Dictionary<char, int> { ['A'] = 9 };
        public IReadOnlyDictionary<char, int> GetTileDistribution(TileDistributionProfile profile) => GetTileDistribution();
        public string GetLanguageName() => name;
        public string GetLocale() => "xx-XX";
    }

    [Fact]
    public void Union_word_valid_in_any_language()
    {
        var fr = new FakeProvider("fr", new HashSet<string> { "CHAT", "MAISON" });
        var en = new FakeProvider("en", new HashSet<string> { "HOUSE", "CAT" });
        var multi = new MultiLanguageProvider([fr, en]);

        var dict = multi.GetDictionary();
        dict.Contains("CHAT").Should().BeTrue();
        dict.Contains("HOUSE").Should().BeTrue();
        dict.Contains("ZZZZ").Should().BeFalse();
    }

    [Fact]
    public void Scores_come_from_primary_language()
    {
        var fr = new FakeProvider("fr", new HashSet<string> { "CHAT" });
        var en = new FakeProvider("en", new HashSet<string> { "CAT" });
        var multi = new MultiLanguageProvider([fr, en]);

        multi.GetLanguageName().Should().Be("fr + en");
        multi.GetLetterScores()['A'].Should().Be(1);
    }
}
