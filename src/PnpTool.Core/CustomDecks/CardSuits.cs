using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Xml;
using System.Xml.Serialization;

namespace PnpTool.Core.CustomDecks;

/// <summary>Original: <c>BarajaPersonalizada.Palo</c>. The first four are Spanish suits, the rest French.</summary>
public enum Suit
{
    [XmlEnum("Oros")] Coins,
    [XmlEnum("Copas")] Cups,
    [XmlEnum("Espadas")] Swords,
    [XmlEnum("Bastos")] Clubs,
    [XmlEnum("Picas")] Spades,
    [XmlEnum("Diamantes")] Diamonds,
    [XmlEnum("Treboles")] FrenchClubs,
    [XmlEnum("Corazones")] Hearts,
}

/// <summary>Original: <c>BarajaPersonalizada.TipoBaraja</c>.</summary>
public enum CardSetType
{
    [XmlEnum("Espanola")] Spanish,
    Poker,
}

/// <summary>Rules for generating Spanish and poker decks. Original: <c>BarajaPersonalizada</c>.</summary>
public static class CustomCardSet
{
    /// <summary>
    /// Label of the nth card of a suit: J, Q, K for 11–13 in poker; 10, 11, 12 for the 8th–10th
    /// card of the Spanish 40-card deck. Original: <c>ActualizaTextoCarta</c>.
    /// </summary>
    public static string CardLabel(int number, CardSetType set) => set == CardSetType.Spanish
        ? number switch
        {
            8 => "10",
            9 => "11",
            10 => "12",
            _ => number.ToString(),
        }
        : number switch
        {
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => number.ToString(),
        };

    /// <summary>
    /// Mask image (Spanish suits only, otherwise empty) and symbol image of a suit. The files
    /// keep their Spanish names, in small letters as they are on disk: the original asks for
    /// "Oros.png", which Windows finds whatever the case and Linux does not.
    /// Original: <c>DevuelveRutaImagenPalo</c>.
    /// </summary>
    public static (string Mask, string Image) SuitImagePaths(Suit suit)
    {
        string file = suit.ToFileName().ToLowerInvariant() + ".png";
        string mask = suit < Suit.Spades ? Path.Combine(AppPaths.SuitsFolder, "mascara_" + file) : string.Empty;
        return (mask, Path.Combine(AppPaths.SuitsFolder, file));
    }

    /// <summary>Red for diamonds and hearts, black otherwise. Original: <c>DevuelveColorPalo</c>.</summary>
    public static string SuitColor(Suit suit) =>
        suit is Suit.Diamonds or Suit.Hearts ? "255_255_0_0" : AppSettings.ColorBlack;

    /// <summary>The suit that follows when generating a deck. Original: <c>CambiaPalo</c>.</summary>
    public static Suit NextSuit(Suit suit) => suit switch
    {
        Suit.Spades => Suit.Diamonds,
        Suit.Diamonds => Suit.FrenchClubs,
        Suit.FrenchClubs => Suit.Hearts,
        Suit.Hearts => Suit.Spades,
        Suit.Coins => Suit.Cups,
        Suit.Cups => Suit.Swords,
        Suit.Swords => Suit.Clubs,
        _ => Suit.Coins,
    };

    /// <summary>Size of the suit symbol on the card. Original: <c>TamanoImagen</c>.</summary>
    public static UnitPoint SymbolSize(Suit suit)
    {
        Unit five = Unit.FromMillimeter(5.0);
        return suit is Suit.Swords or Suit.Clubs
            ? new UnitPoint(five, Unit.FromMillimeter(7.0))
            : new UnitPoint(five, five);
    }
}
