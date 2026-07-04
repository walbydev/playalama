using System.Reflection;
using System.Text.Json;

namespace Lama.Languages.en.UnitTests;

/// <summary>
/// Validates that the English language pack embedded assets are well-formed
/// and contain the expected data structure.
/// </summary>
public class EnglishAssetTests
{
    private static readonly Assembly Assembly = System.Reflection.Assembly.Load("Lama.Languages.en");

    private static JsonDocument LoadEmbeddedResource(string name)
    {
        var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded resource '{name}' not found.");
        return JsonDocument.Parse(stream);
    }

    [Fact]
    public void ScoresJson_EmbeddedResource_Exists()
    {
        var act = () => LoadEmbeddedResource("Lama.Languages.en.assets.scores.json");
        act.Should().NotThrow();
    }

    [Fact]
    public void TileDistributionJson_EmbeddedResource_Exists()
    {
        var act = () => LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        act.Should().NotThrow();
    }

    [Fact]
    public void ScoresJson_ContainsAll26Letters()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.scores.json");
        var scores = doc.RootElement.GetProperty("scores");

        for (var c = 'A'; c <= 'Z'; c++)
        {
            scores.TryGetProperty(c.ToString(), out var score).Should().BeTrue(
                $"letter '{c}' should have a score in English scores.json");
            score.GetInt32().Should().BeGreaterThan(0,
                $"letter '{c}' score should be positive");
        }
    }

    [Fact]
    public void ScoresJson_EnglishSpecificValues_AreCorrect()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.scores.json");
        var scores = doc.RootElement.GetProperty("scores");

        // Known English Scrabble values
        scores.GetProperty("A").GetInt32().Should().Be(1);
        scores.GetProperty("Q").GetInt32().Should().Be(10);
        scores.GetProperty("Z").GetInt32().Should().Be(10);
    }

    [Fact]
    public void TileDistributionJson_HasBaseDistribution()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var baseDist = doc.RootElement.GetProperty("baseDistribution");

        baseDist.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public void TileDistributionJson_ContainsJokers()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var baseDist = doc.RootElement.GetProperty("baseDistribution");

        baseDist.TryGetProperty("*", out var jokers).Should().BeTrue("jokers should be in distribution");
        jokers.GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TileDistributionJson_ContainsAll26Letters()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var baseDist = doc.RootElement.GetProperty("baseDistribution");

        for (var c = 'A'; c <= 'Z'; c++)
        {
            baseDist.TryGetProperty(c.ToString(), out var count).Should().BeTrue(
                $"letter '{c}' should be in English tile distribution");
            count.GetInt32().Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void TileDistributionJson_Total_IsReasonable()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var baseDist = doc.RootElement.GetProperty("baseDistribution");

        var total = baseDist.EnumerateObject().Sum(p => p.Value.GetInt32());
        total.Should().BeGreaterThan(80, "English Scrabble has 100 tiles");
        total.Should().BeLessThan(200, "total tile count should be reasonable");
    }

    [Fact]
    public void TileDistributionJson_HasScalingSection()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var act = () => doc.RootElement.GetProperty("scaling");
        act.Should().NotThrow();
    }

    [Fact]
    public void TileDistributionJson_Scaling_HasRequiredFields()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var scaling = doc.RootElement.GetProperty("scaling");

        scaling.GetProperty("minMultiplier").GetDouble().Should().BeGreaterThan(0);
        scaling.GetProperty("maxMultiplier").GetDouble().Should().BeGreaterThan(0);
        scaling.GetProperty("boardExponent").GetDouble().Should().BeGreaterThan(0);
        scaling.GetProperty("boardReferenceSize").GetInt32().Should().BeGreaterThan(0);
        scaling.GetProperty("rackReferenceSize").GetInt32().Should().BeGreaterThan(0);
        scaling.GetProperty("rackWeight").GetDouble().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void TileDistributionJson_Scaling_HasGameTypeMultipliers()
    {
        using var doc = LoadEmbeddedResource("Lama.Languages.en.assets.tile-distribution.json");
        var scaling = doc.RootElement.GetProperty("scaling");
        var gameTypes = scaling.GetProperty("gameTypeMultipliers");

        gameTypes.EnumerateObject().Should().NotBeEmpty();
        gameTypes.TryGetProperty("classic", out _).Should().BeTrue("classic game type should be defined");
    }
}
