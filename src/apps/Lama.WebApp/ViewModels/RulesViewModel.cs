namespace Lama.WebApp.ViewModels;

public sealed class RulesViewModel
{
    public IReadOnlyList<RulesSection> Sections { get; } =
    [
        new("common", "Rules.Section.Common.Title", "Rules.Section.Common.Intro", "",
        [
            new("Rules.Section.Common.R0.Title", "Rules.Section.Common.R0.Text"),
            new("Rules.Section.Common.R1.Title", "Rules.Section.Common.R1.Text"),
            new("Rules.Section.Common.R2.Title", "Rules.Section.Common.R2.Text"),
            new("Rules.Section.Common.R3.Title", "Rules.Section.Common.R3.Text"),
            new("Rules.Section.Common.R4.Title", "Rules.Section.Common.R4.Text"),
            new("Rules.Section.Common.R5.Title", "Rules.Section.Common.R5.Text"),
            new("Rules.Section.Common.R6.Title", "Rules.Section.Common.R6.Text"),
            new("Rules.Section.Common.R7.Title", "Rules.Section.Common.R7.Text"),
            new("Rules.Section.Common.R8.Title", "Rules.Section.Common.R8.Text"),
            new("Rules.Section.Common.R9.Title", "Rules.Section.Common.R9.Text"),
            new("Rules.Section.Common.R10.Title", "Rules.Section.Common.R10.Text"),
            new("Rules.Section.Common.R11.Title", "Rules.Section.Common.R11.Text"),
        ]),

        new("classique", "Rules.Section.Classique.Title", "Rules.Section.Classique.Intro", "Rules.Section.Classique.ModeName",
        [
            new("Rules.Section.Classique.R0.Title", "Rules.Section.Classique.R0.Text"),
            new("Rules.Section.Classique.R1.Title", "Rules.Section.Classique.R1.Text"),
            new("Rules.Section.Classique.R2.Title", "Rules.Section.Classique.R2.Text"),
            new("Rules.Section.Classique.R3.Title", "Rules.Section.Classique.R3.Text"),
        ]),

        new("blitz", "Rules.Section.Blitz.Title", "Rules.Section.Blitz.Intro", "Rules.Section.Blitz.ModeName",
        [
            new("Rules.Section.Blitz.R0.Title", "Rules.Section.Blitz.R0.Text"),
            new("Rules.Section.Blitz.R1.Title", "Rules.Section.Blitz.R1.Text"),
            new("Rules.Section.Blitz.R2.Title", "Rules.Section.Blitz.R2.Text"),
            new("Rules.Section.Blitz.R3.Title", "Rules.Section.Blitz.R3.Text"),
            new("Rules.Section.Blitz.R4.Title", "Rules.Section.Blitz.R4.Text"),
        ]),

        new("solo-ia", "Rules.Section.SoloIa.Title", "Rules.Section.SoloIa.Intro", "Rules.Section.SoloIa.ModeName",
        [
            new("Rules.Section.SoloIa.R0.Title", "Rules.Section.SoloIa.R0.Text"),
            new("Rules.Section.SoloIa.R1.Title", "Rules.Section.SoloIa.R1.Text"),
            new("Rules.Section.SoloIa.R2.Title", "Rules.Section.SoloIa.R2.Text"),
        ]),

        new("2v2", "Rules.Section.Team.Title", "Rules.Section.Team.Intro", "Rules.Section.Team.ModeName",
        [
            new("Rules.Section.Team.R0.Title", "Rules.Section.Team.R0.Text"),
            new("Rules.Section.Team.R1.Title", "Rules.Section.Team.R1.Text"),
            new("Rules.Section.Team.R2.Title", "Rules.Section.Team.R2.Text"),
            new("Rules.Section.Team.R3.Title", "Rules.Section.Team.R3.Text"),
        ]),

        new("grand-plateau", "Rules.Section.GrandPlateau.Title", "Rules.Section.GrandPlateau.Intro", "Rules.Section.GrandPlateau.ModeName",
        [
            new("Rules.Section.GrandPlateau.R0.Title", "Rules.Section.GrandPlateau.R0.Text"),
            new("Rules.Section.GrandPlateau.R1.Title", "Rules.Section.GrandPlateau.R1.Text"),
            new("Rules.Section.GrandPlateau.R2.Title", "Rules.Section.GrandPlateau.R2.Text"),
            new("Rules.Section.GrandPlateau.R3.Title", "Rules.Section.GrandPlateau.R3.Text"),
        ]),

        new("chaos", "Rules.Section.Chaos.Title", "Rules.Section.Chaos.Intro", "Rules.Section.Chaos.ModeName",
        [
            new("Rules.Section.Chaos.R0.Title", "Rules.Section.Chaos.R0.Text"),
            new("Rules.Section.Chaos.R1.Title", "Rules.Section.Chaos.R1.Text"),
            new("Rules.Section.Chaos.R2.Title", "Rules.Section.Chaos.R2.Text"),
            new("Rules.Section.Chaos.R3.Title", "Rules.Section.Chaos.R3.Text"),
            new("Rules.Section.Chaos.R4.Title", "Rules.Section.Chaos.R4.Text"),
        ]),
    ];

    public string? ActiveSectionId { get; private set; }

    public void SetActiveSection(string? id) => ActiveSectionId = id;
}

public sealed record RulesSection(
    string Id,
    string TitleKey,
    string IntroKey,
    string ModeNameKey,
    IReadOnlyList<RulesRule> Rules);

public sealed record RulesRule(string TitleKey, string TextKey);
