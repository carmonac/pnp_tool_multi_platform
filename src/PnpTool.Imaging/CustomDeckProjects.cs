using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>
/// Turns a custom deck or a sports deck into a project ready for its PDF: the image of every
/// card is made in a folder and the cards are laid out with the style of the deck.
/// Original: <c>WindowCrearMazoPersonalizadoModelo</c> and <c>WindowCrearMazoPersonalizadoDeportesModelo</c>.
/// </summary>
public static class CustomDeckProjects
{
    /// <summary>The name the project of a deck is saved with, in the folder of its images.</summary>
    public const string ProjectFileName = "MazoPersonalizado.pnp";

    /// <summary>
    /// Makes the images of a deck of the creator, the picture of each card over the background
    /// chosen for it, and the project that prints them with the back of the deck.
    /// Original: <c>WindowCrearMazoPersonalizadoModelo.Procesar</c>.
    /// </summary>
    /// <param name="deck">The deck; the merged image of every card is noted in it.</param>
    /// <param name="folder">Where the images are written.</param>
    /// <param name="progress">Told the share done, from 0 to 100.</param>
    public static (AppError Error, Project Project) Build(CustomDeck deck, string folder, Action<double>? progress = null)
    {
        Directory.CreateDirectory(folder);
        for (int index = 0; index < deck.Cards.Length; index++)
        {
            progress?.Invoke(100.0 * index / deck.Cards.Length);
            CustomCardImages.Merge(deck.Cards[index], folder, AppPaths.CustomDecksFolder);
        }

        var settings = new ProjectSettings();
        AppError error = ProjectFileStore.LoadStyle(Path.Combine(AppPaths.DefaultStylesFolder, deck.Style), ref settings).Error;
        return (error, Finish(deck, settings));
    }

    /// <summary>
    /// Makes the images of a sports deck: the template over the picture of each player, with
    /// the name of the team and the name of the player written on it.
    /// Original: <c>WindowCrearMazoPersonalizadoDeportesModelo.Procesar</c>.
    /// </summary>
    /// <remarks>
    /// Two differences from the original, which took both texts from the boxes of its window:
    /// every card carries the name of its own player (the original wrote on all of them the
    /// name of the player on show), and the name of the team is left out when the deck says
    /// it is not shown (the original wrote it all the same).
    /// </remarks>
    public static (AppError Error, Project Project) BuildSports(SportsDeck deck, string folder, Action<double>? progress = null)
    {
        Directory.CreateDirectory(folder);
        for (int index = 0; index < deck.Cards.Length; index++)
        {
            progress?.Invoke(100.0 * index / deck.Cards.Length);
            CustomCard card = deck.Cards[index];
            CustomCardImages.MergeSports(card, folder, deck.TemplateImagePath);
            if (deck.TeamTextVisible)
            {
                CustomCardImages.AddText(card, deck.TeamText.Text, deck.TeamText.Font, deck.TeamText.Position);
            }

            CustomCardImages.AddText(card, card.NameSpanish, deck.PlayerText.Font, deck.PlayerText.Position);
        }

        (AppError error, ProjectSettings settings) = SportsSettings();
        return (error, Finish(deck, settings));
    }

    /// <summary>
    /// The layout of a sports deck: cards of 63 by 88 mm with a mirrored border of one
    /// millimetre, two millimetres apart, with cut lines. The original read these values from
    /// a style of the printing company, which is not shipped; its marks, numbering, file
    /// identifier and CMYK output are left out.
    /// </summary>
    public static (AppError Error, ProjectSettings Settings) SportsSettings()
    {
        var settings = new ProjectSettings();
        AppError error = ProjectFileStore.LoadStyle(Path.Combine(AppPaths.DefaultStylesFolder, "Cartas_Standar.pnps"), ref settings).Error;
        var face = new UnitPoint { X = Unit.FromMillimeter(63.0), Y = Unit.FromMillimeter(88.0) };
        foreach (FaceSettings side in new[] { settings.Front, settings.Back })
        {
            side.Size = new UnitPoint(face);
            side.Effect = ImageEffectType.Mirror;
            side.CutLine.LineType = CutLineType.Normal;
        }

        settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(65.0), Y = Unit.FromMillimeter(90.0) };
        settings.Grid.Spacing = new UnitPoint { X = Unit.FromMillimeter(2.0), Y = Unit.FromMillimeter(2.0) };
        settings.Grid.Bleed = Unit.FromMillimeter(1.0);
        settings.Group.Group = SettingsGroup.Custom;
        return (error, settings);
    }

    /// <summary>Double-sided, one face a card, with the back of the deck behind every card.</summary>
    private static Project Finish(CustomDeck deck, ProjectSettings settings)
    {
        settings.PrintLayout.DoubleSided = true;
        settings.DoublePanel = false;
        settings.SharedBack = new CardFace(deck.BackImagePath, FaceType.Back, ImageKind.Normal);
        return new Project { Deck = new Deck(deck.Cards), Settings = settings };
    }
}
