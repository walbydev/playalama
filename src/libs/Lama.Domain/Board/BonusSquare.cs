namespace Lama.Domain.Board;

/// <summary>
/// Type de case bonus sur le plateau.
/// </summary>
public enum BonusType
{
    /// <summary>Case normale, pas de bonus.</summary>
    None,

    /// <summary>Lettre compte double (case bleu clair).</summary>
    DoubleLetter,

    /// <summary>Lettre compte triple (case bleu foncé).</summary>
    TripleLetter,

    /// <summary>Mot compte double (case rose).</summary>
    DoubleWord,

    /// <summary>Mot compte triple (case rouge).</summary>
    TripleWord,

    /// <summary>Case de départ — équivaut à DoubleWord (H8).</summary>
    Start
}

/// <summary>
/// Représente une case bonus sur le plateau.
/// </summary>
public readonly record struct BonusSquare(BonusType Type)
{
    /// <summary>
    /// Multiplicateur de lettre pour cette case (1, 2 ou 3).
    /// </summary>
    public int LetterMultiplier => Type switch
    {
        BonusType.DoubleLetter => 2,
        BonusType.TripleLetter => 3,
        _                      => 1
    };

    /// <summary>
    /// Multiplicateur de mot pour cette case (1, 2 ou 3).
    /// </summary>
    public int WordMultiplier => Type switch
    {
        BonusType.DoubleWord => 2,
        BonusType.TripleWord => 3,
        BonusType.Start      => 2,
        _                    => 1
    };

    public static readonly BonusSquare None         = new(BonusType.None);
    public static readonly BonusSquare DoubleLetter = new(BonusType.DoubleLetter);
    public static readonly BonusSquare TripleLetter = new(BonusType.TripleLetter);
    public static readonly BonusSquare DoubleWord   = new(BonusType.DoubleWord);
    public static readonly BonusSquare TripleWord   = new(BonusType.TripleWord);
    public static readonly BonusSquare Start        = new(BonusType.Start);
}
