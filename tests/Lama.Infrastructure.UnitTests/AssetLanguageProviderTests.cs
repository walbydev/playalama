using FluentAssertions;
using Lama.Contracts;
using Lama.Contracts.Lexicon;
using Lama.Infrastructure.Lexicon;
using Moq;

namespace Lama.Infrastructure.UnitTests;

public class AssetLanguageProviderTests
{
    private const string TileDistributionJson = """
        {
          "baseDistribution": {
            "A": 9, "B": 2, "C": 2, "D": 3, "E": 15,
            "F": 2, "G": 2, "H": 2, "I": 8, "J": 1,
            "K": 1, "L": 5, "M": 3, "N": 6, "O": 6,
            "P": 2, "Q": 1, "R": 6, "S": 6, "T": 6,
            "U": 6, "V": 2, "W": 1, "X": 1, "Y": 1,
            "Z": 1, "*": 2
          },
          "scaling": {
            "minMultiplier": 0.7,
            "maxMultiplier": 1.8,
            "boardExponent": 1.0,
            "boardReferenceSize": 15,
            "rackReferenceSize": 7,
            "rackWeight": 0.2,
            "gameTypeMultipliers": {
              "classic": 1.0,
              "duplicate": 0.95,
              "blitz": 0.88,
              "tournament": 1.0
            },
            "levelMultipliers": {
              "Casual": 1.04,
              "Standard": 1.0,
              "Competitive": 0.97,
              "Tournament": 0.95
            }
          }
        }
        """;

    private static string CreateTempBasePath(string scoresJson)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LamaTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "scores.json"), scoresJson);
        File.WriteAllText(Path.Combine(tempRoot, "tile-distribution.json"), TileDistributionJson);
        return tempRoot;
    }

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { }
    }

    [Fact]
    public void Constructor_LoadsLetterScoresFromDisk()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1, \"B\": 3, \"Z\": 10 } }");
        try
        {
            var provider = new AssetLanguageProvider("fr", "Français", "fr-FR", tmp, new HashSet<string>());

            provider.GetLetterScores()['A'].Should().Be(1);
            provider.GetLetterScores()['B'].Should().Be(3);
            provider.GetLetterScores()['Z'].Should().Be(10);
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void Constructor_LoadsTileDistributionFromDisk()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        try
        {
            var provider = new AssetLanguageProvider("fr", "Français", "fr-FR", tmp, new HashSet<string>());

            var dist = provider.GetTileDistribution();
            dist.Values.Sum().Should().Be(102);
            dist['*'].Should().Be(2);
            dist['E'].Should().Be(15);
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetDictionary_ReturnsInjectedSet()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        var dict = new HashSet<string> { "BONJOUR", "SALUT" };
        try
        {
            var provider = new AssetLanguageProvider("fr", "Français", "fr-FR", tmp, dict);
            provider.GetDictionary().Should().BeSameAs(dict);
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetLanguageName_ReturnsProvidedName()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        try
        {
            var provider = new AssetLanguageProvider("de", "Deutsch", "de-DE", tmp, new HashSet<string>());
            provider.GetLanguageName().Should().Be("Deutsch");
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetLocale_ReturnsProvidedLocale()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        try
        {
            var provider = new AssetLanguageProvider("en", "English", "en-US", tmp, new HashSet<string>());
            provider.GetLocale().Should().Be("en-US");
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetTileDistribution_WithProfile_BiggerBoard_ProducesMoreTiles()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        try
        {
            var provider = new AssetLanguageProvider("fr", "Français", "fr-FR", tmp, new HashSet<string>());
            var classic = provider.GetTileDistribution(new TileDistributionProfile("fr", 15, 7, GameLevel.Standard, "classic"));
            var bigger = provider.GetTileDistribution(new TileDistributionProfile("fr", 17, 7, GameLevel.Standard, "classic"));
            bigger.Values.Sum().Should().BeGreaterThan(classic.Values.Sum());
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetTileDistribution_WithProfile_Blitz_ReducesTiles()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        try
        {
            var provider = new AssetLanguageProvider("fr", "Français", "fr-FR", tmp, new HashSet<string>());
            var classic = provider.GetTileDistribution(new TileDistributionProfile("fr", 15, 7, GameLevel.Standard, "classic"));
            var blitz = provider.GetTileDistribution(new TileDistributionProfile("fr", 15, 7, GameLevel.Standard, "blitz"));
            blitz.Values.Sum().Should().BeLessThan(classic.Values.Sum());
        }
        finally { SafeDelete(tmp); }
    }
}

public class LanguageProviderRegistryTests
{
    [Fact]
    public void SupportedLanguages_ContainsFr_En_De()
    {
        var reader = new Mock<ILexiconReader>();
        var registry = new LanguageProviderRegistry(reader.Object, "/tmp/assets");

        registry.SupportedLanguages.Should().Contain(["fr", "en", "de"]);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("FR")]
    [InlineData("EN")]
    public void IsSupported_WithKnownCode_ReturnsTrue(string code)
    {
        var reader = new Mock<ILexiconReader>();
        var registry = new LanguageProviderRegistry(reader.Object, "/tmp/assets");

        registry.IsSupported(code).Should().BeTrue();
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("it")]
    [InlineData("")]
    public void IsSupported_WithUnknownCode_ReturnsFalse(string code)
    {
        var reader = new Mock<ILexiconReader>();
        var registry = new LanguageProviderRegistry(reader.Object, "/tmp/assets");

        registry.IsSupported(code).Should().BeFalse();
    }

    [Fact]
    public void GetProvider_WithSingleCode_ReturnsAssetLanguageProvider()
    {
        var reader = new Mock<ILexiconReader>();
        reader.Setup(r => r.LoadDictionary(It.IsAny<string>())).Returns(new HashSet<string>());
        var tmp = CreateTempAssets("fr");
        try
        {
            var registry = new LanguageProviderRegistry(reader.Object, tmp);
            var provider = registry.GetProvider("fr");

            provider.Should().NotBeNull();
            provider.GetLanguageName().Should().Be("Français");
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetProvider_CachesProvider()
    {
        var reader = new Mock<ILexiconReader>();
        reader.Setup(r => r.LoadDictionary(It.IsAny<string>())).Returns(new HashSet<string>());
        var tmp = CreateTempAssets("fr");
        try
        {
            var registry = new LanguageProviderRegistry(reader.Object, tmp);
            var p1 = registry.GetProvider("fr");
            var p2 = registry.GetProvider("fr");

            p1.Should().BeSameAs(p2, because: "provider should be cached");
            reader.Verify(r => r.LoadDictionary("fr"), Times.Once);
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetProvider_WithMultipleCodes_ReturnsMultiLanguageProvider()
    {
        var reader = new Mock<ILexiconReader>();
        reader.Setup(r => r.LoadDictionary(It.IsAny<string>())).Returns(new HashSet<string>());
        var tmp = CreateTempAssets("fr", "en");
        try
        {
            var registry = new LanguageProviderRegistry(reader.Object, tmp);
            var provider = registry.GetProvider(new List<string> { "fr", "en" });

            provider.Should().NotBeNull();
            // MultiLanguageProvider merges dictionaries
            provider.GetDictionary().Should().NotBeNull();
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetProvider_WithSingleCodeInList_ReturnsSingleProvider()
    {
        var reader = new Mock<ILexiconReader>();
        reader.Setup(r => r.LoadDictionary(It.IsAny<string>())).Returns(new HashSet<string>());
        var tmp = CreateTempAssets("fr");
        try
        {
            var registry = new LanguageProviderRegistry(reader.Object, tmp);
            var provider = registry.GetProvider(new List<string> { "fr" });

            provider.GetLanguageName().Should().Be("Français");
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetProvider_WithEmptyCodeList_DefaultsToFr()
    {
        var reader = new Mock<ILexiconReader>();
        reader.Setup(r => r.LoadDictionary(It.IsAny<string>())).Returns(new HashSet<string>());
        var tmp = CreateTempAssets("fr");
        try
        {
            var registry = new LanguageProviderRegistry(reader.Object, tmp);
            var provider = registry.GetProvider(new List<string>());

            provider.GetLanguageName().Should().Be("Français");
        }
        finally { SafeDelete(tmp); }
    }

    [Fact]
    public void GetProvider_WithOnlyUnsupportedCodes_DefaultsToFr()
    {
        var reader = new Mock<ILexiconReader>();
        reader.Setup(r => r.LoadDictionary(It.IsAny<string>())).Returns(new HashSet<string>());
        var tmp = CreateTempAssets("fr");
        try
        {
            var registry = new LanguageProviderRegistry(reader.Object, tmp);
            var provider = registry.GetProvider(new List<string> { "xx", "it" });

            provider.GetLanguageName().Should().Be("Français");
        }
        finally { SafeDelete(tmp); }
    }

    private static string CreateTempAssets(params string[] codes)
    {
        var root = Path.Combine(Path.GetTempPath(), "LamaTests", Guid.NewGuid().ToString("N"));
        foreach (var code in codes)
        {
            var dir = Path.Combine(root, "assets", "languages", code);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "scores.json"), "{ \"scores\": { \"A\": 1 } }");
            File.WriteAllText(Path.Combine(dir, "tile-distribution.json"), """
                {
                  "baseDistribution": { "A": 9, "B": 2, "*": 2 },
                  "scaling": {
                    "minMultiplier": 0.7, "maxMultiplier": 1.8, "boardExponent": 1.0,
                    "boardReferenceSize": 15, "rackReferenceSize": 7, "rackWeight": 0.2,
                    "gameTypeMultipliers": { "classic": 1.0 },
                    "levelMultipliers": { "Standard": 1.0 }
                  }
                }
                """);
        }
        return root;
    }

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { }
    }
}
