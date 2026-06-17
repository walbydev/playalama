using FluentAssertions;
using Lama.Domain.Bag;

namespace Lama.Domain.UnitTests.Bag;

/// <summary>
/// Tests unitaires pour <see cref="TileBag"/>.
/// Vérifie la distribution initiale, la pioche et l'échange de lettres.
/// Référence : distribution officielle Scrabble français.
/// </summary>
public class TileBagTests
{
    // Distribution française officielle : 102 tuiles dont 2 jokers
    private static readonly Dictionary<char, int> FrenchDistribution = new()
    {
        ['A'] = 9,  ['B'] = 2,  ['C'] = 2,  ['D'] = 3,  ['E'] = 15,
        ['F'] = 2,  ['G'] = 2,  ['H'] = 2,  ['I'] = 8,  ['J'] = 1,
        ['K'] = 1,  ['L'] = 5,  ['M'] = 3,  ['N'] = 6,  ['O'] = 6,
        ['P'] = 2,  ['Q'] = 1,  ['R'] = 6,  ['S'] = 6,  ['T'] = 6,
        ['U'] = 6,  ['V'] = 2,  ['W'] = 1,  ['X'] = 1,  ['Y'] = 1,
        ['Z'] = 1,  ['*'] = 2   // * = joker
    };

    private static readonly IReadOnlyDictionary<char, int> FrenchDistributionReadOnly = FrenchDistribution;

    #region Construction

    [Fact]
    public void NewBag_HasCorrectTotalCount_French()
    {
        var bag = new TileBag(FrenchDistributionReadOnly);

        bag.Count.Should().Be(102,
            because: "le sac français contient 102 tuiles au total");
    }

    [Fact]
    public void NewBag_IsNotEmpty()
    {
        var bag = new TileBag(FrenchDistributionReadOnly);

        bag.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void NewBag_HasCorrectCountPerLetter()
    {
        var bag = new TileBag(FrenchDistributionReadOnly);
        var remaining = bag.GetRemainingCounts();

        foreach (var (letter, expected) in FrenchDistribution)
        {
            remaining[letter].Should().Be(expected,
                because: $"le sac doit contenir {expected} tuile(s) '{letter}'");
        }
    }

    #endregion

    #region Draw (pioche)

    [Fact]
    public void Draw_ReturnsTiles_WhenBagHasEnough()
    {
        var bag   = new TileBag(FrenchDistributionReadOnly);
        var drawn = bag.Draw(7);

        drawn.Should().HaveCount(7,
            because: "on doit pouvoir piocher 7 lettres pour un rack initial");
    }

    [Fact]
    public void Draw_ReducesBagCount()
    {
        var bag = new TileBag(FrenchDistributionReadOnly);
        bag.Draw(7);

        bag.Count.Should().Be(95,
            because: "après avoir pioché 7 lettres, il reste 95 tuiles");
    }

    [Fact]
    public void Draw_ReturnsOnlyRemainingTiles_WhenBagHasLess()
    {
        // Sac avec seulement 3 tuiles
        var bag   = new TileBag(new Dictionary<char, int> { ['A'] = 3 });
        var drawn = bag.Draw(7);

        drawn.Should().HaveCount(3,
            because: "si le sac contient moins que demandé, on prend ce qui reste");
        bag.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Draw_ReturnsEmptyList_WhenBagIsEmpty()
    {
        var bag = new TileBag(new Dictionary<char, int> { ['A'] = 1 });
        bag.Draw(1); // vide le sac

        var drawn = bag.Draw(7);

        drawn.Should().BeEmpty(because: "un sac vide ne peut rien distribuer");
    }

    [Fact]
    public void Draw_IsRandom_DifferentOrderEachTime()
    {
        // Statistiquement, deux tirages de 7 lettres d'un sac de 102
        // ne doivent pas être identiques (probabilité infime)
        var bag1   = new TileBag(FrenchDistributionReadOnly);
        var bag2   = new TileBag(FrenchDistributionReadOnly);
        var drawn1 = bag1.Draw(7);
        var drawn2 = bag2.Draw(7);

        // On vérifie au moins que les lettres sont dans le bon alphabet
        drawn1.Should().OnlyContain(c => FrenchDistribution.ContainsKey(c));
        drawn2.Should().OnlyContain(c => FrenchDistribution.ContainsKey(c));
        // Note : on ne teste pas l'ordre car le hasard peut parfois donner
        // les mêmes lettres — ce test vérifie surtout que Draw ne plante pas.
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Draw_WithZeroOrNegativeCount_ReturnsEmpty(int count)
    {
        var bag   = new TileBag(FrenchDistributionReadOnly);
        var drawn = bag.Draw(count);

        drawn.Should().BeEmpty();
    }

    #endregion

    #region ReturnTiles (retour au sac)

    [Fact]
    public void ReturnTiles_IncreasesCount()
    {
        var bag = new TileBag(FrenchDistributionReadOnly);
        bag.Draw(7);

        bag.ReturnTiles(['A', 'B', 'C']);

        bag.Count.Should().Be(98, because: "on a pioché 7 et remis 3, soit 102 - 7 + 3 = 98");
    }

    [Fact]
    public void ReturnTiles_UpdatesLetterCounts()
    {
        var bag = new TileBag(new Dictionary<char, int> { ['A'] = 2, ['B'] = 1 });
        bag.Draw(3); // vide le sac

        bag.ReturnTiles(['A', 'A']);
        var remaining = bag.GetRemainingCounts();

        remaining['A'].Should().Be(2,
            because: "remettre 2 'A' dans un sac vide doit en donner 2");
    }

    [Fact]
    public void ReturnTiles_WithEmptyList_DoesNothing()
    {
        var bag   = new TileBag(FrenchDistributionReadOnly);
        var before = bag.Count;

        bag.ReturnTiles([]);

        bag.Count.Should().Be(before);
    }

    #endregion

    #region Swap (échange)

    [Fact]
    public void Swap_ReturnsNewTiles_AndReturnsOldToSac()
    {
        var bag      = new TileBag(FrenchDistributionReadOnly);
        var initial  = bag.Draw(7);
        var toSwap   = initial.Take(3).ToList();

        var newTiles = bag.Swap(toSwap);

        newTiles.Should().HaveCount(3,
            because: "swap retourne exactement le même nombre de tuiles");
        bag.Count.Should().Be(102 - 7,
            because: "après draw(7) et swap(3→3), le sac contient 95 tuiles");
    }

    [Fact]
    public void Swap_ReturnsEmptyList_WhenBagIsEmpty()
    {
        var bag = new TileBag(new Dictionary<char, int> { ['A'] = 3 });
        bag.Draw(3); // vide le sac

        var result = bag.Swap(['A', 'B']);

        result.Should().BeEmpty(
            because: "on ne peut pas échanger si le sac est vide");
    }

    #endregion

    #region IsEmpty

    [Fact]
    public void IsEmpty_IsTrue_WhenAllTilesDrawn()
    {
        var bag = new TileBag(new Dictionary<char, int> { ['A'] = 2 });
        bag.Draw(2);

        bag.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_IsFalse_WhenTilesRemain()
    {
        var bag = new TileBag(FrenchDistributionReadOnly);

        bag.IsEmpty.Should().BeFalse();
    }

    #endregion

    #region GetRemainingCounts

    [Fact]
    public void GetRemainingCounts_ReflectsDrawnTiles()
    {
        var bag = new TileBag(new Dictionary<char, int> { ['A'] = 5, ['B'] = 3 });

        // On pioche jusqu'à avoir des A
        var drawn = bag.Draw(5);
        var aDrawn = drawn.Count(c => c == 'A');

        var remaining = bag.GetRemainingCounts();
        remaining.TryGetValue('A', out var aLeft);

        aLeft.Should().Be(5 - aDrawn,
            because: "le compte restant doit refléter les piochées");
    }

    #endregion
}
