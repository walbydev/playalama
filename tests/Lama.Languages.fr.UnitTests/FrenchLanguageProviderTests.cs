using Lama.Contracts;

namespace Lama.Languages.fr.UnitTests;

public class FrenchLanguageProviderTests
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

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    [Fact]
    public void GetDictionary_ReturnsInjectedSet()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");
        var dict = new HashSet<string>(StringComparer.Ordinal) { "BONJOUR", "SALUT" };

        try
        {
            var provider = new FrenchLanguageProvider(dict, tmp);
            Assert.Same(dict, provider.GetDictionary());
        }
        finally
        {
            SafeDeleteDirectory(tmp);
        }
    }

    [Fact]
    public void GetLetterScores_LoadsFromAssets()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1, \"B\": 3, \"Z\": 10 } }");

        try
        {
            var provider = new FrenchLanguageProvider(new HashSet<string>(), tmp);
            var scores = provider.GetLetterScores();
            Assert.Equal(1, scores['A']);
            Assert.Equal(3, scores['B']);
            Assert.Equal(10, scores['Z']);
        }
        finally
        {
            SafeDeleteDirectory(tmp);
        }
    }

    [Fact]
    public void GetTileDistribution_UsesExpectedFrenchTotals()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");

        try
        {
            var provider = new FrenchLanguageProvider(new HashSet<string>(), tmp);
            var distribution = provider.GetTileDistribution();
            Assert.Equal(102, distribution.Values.Sum());
            Assert.Equal(2, distribution['*']);
            Assert.Equal(15, distribution['E']);
        }
        finally
        {
            SafeDeleteDirectory(tmp);
        }
    }

    [Fact]
    public void GetTileDistribution_WithProfile_ChangesTotal()
    {
        var tmp = CreateTempBasePath("{ \"scores\": { \"A\": 1 } }");

        try
        {
            var provider = new FrenchLanguageProvider(new HashSet<string>(), tmp);
            var classic = provider.GetTileDistribution(new TileDistributionProfile("fr", 15, 7, GameLevel.Standard, "classic"));
            var biggerBoard = provider.GetTileDistribution(new TileDistributionProfile("fr", 17, 7, GameLevel.Standard, "classic"));
            Assert.True(biggerBoard.Values.Sum() > classic.Values.Sum());
        }
        finally
        {
            SafeDeleteDirectory(tmp);
        }
    }
}
