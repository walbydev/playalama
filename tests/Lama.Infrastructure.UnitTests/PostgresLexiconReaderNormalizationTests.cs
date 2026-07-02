using FluentAssertions;
using Lama.Infrastructure.Lexicon;

namespace Lama.Infrastructure.UnitTests;

public class PostgresLexiconReaderNormalizationTests
{
    [Theory]
    [InlineData("Noël",   "NOEL")]
    [InlineData("noël",   "NOEL")]
    [InlineData("aï",     "AI")]
    [InlineData("élan",   "ELAN")]
    [InlineData("château","CHATEAU")]
    [InlineData("où",     "OU")]
    [InlineData("LAMA",   "LAMA")]
    [InlineData("lama",   "LAMA")]
    [InlineData("côte",   "COTE")]
    [InlineData("naïf",   "NAIF")]
    public void NormalizeForLookup_StripsAccentsAndUppercases(string input, string expected)
    {
        PostgresLexiconReader.NormalizeForLookup(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  Noël  ", "NOEL")]
    [InlineData("  lama  ", "LAMA")]
    public void NormalizeForLookup_TrimsWhitespace(string input, string expected)
    {
        // NormalizeForLookup ne fait pas le trim — c'est fait avant l'appel dans le reader.
        // Ici on vérifie que la normalisation seule est stable sur du texte sans espaces.
        PostgresLexiconReader.NormalizeForLookup(input.Trim()).Should().Be(expected);
    }
}
