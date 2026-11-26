using System.Text;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Cards;

/// <summary>
/// The largest, smallest, average and median size of the images of a deck, which the layout
/// window offers as card sizes. Original: <c>DatosEstadisticos</c>.
/// </summary>
public sealed class DeckStatistics
{
    public UnitPoint Maximum { get; } = new();

    public UnitPoint Minimum { get; } = new();

    /// <summary>Original: <c>Media</c>.</summary>
    public UnitPoint Mean { get; } = new();

    /// <summary>Original: <c>Mediana</c>.</summary>
    public UnitPoint Median { get; } = new();

    public DeckStatistics()
    {
    }

    /// <summary>
    /// Measures the fronts of the deck and, when every card has its own back, the backs too.
    /// Original: <c>ActualizaDatosEstadisticos</c>.
    /// </summary>
    /// <remarks>
    /// Kept from the original: the first face sets largest and smallest even if it has no
    /// size, faces without size count in the average and the median, and the median of an
    /// even number of sizes is the upper of the two in the middle.
    /// </remarks>
    public DeckStatistics(Deck deck, BackImageMode backImage)
    {
        if (deck.Cards.Length == 0)
        {
            return;
        }

        var sizes = new List<UnitPoint>(deck.Cards.Select(card => card.Front.Size));
        if (backImage == BackImageMode.DifferentBack)
        {
            sizes.AddRange(deck.Cards.Select(card => card.Back.Size));
        }

        Maximum = new UnitPoint(sizes[0]);
        Minimum = new UnitPoint(sizes[0]);
        foreach (UnitPoint size in sizes.Skip(1))
        {
            if ((double)size.X > 0.0)
            {
                Maximum.X = Math.Max(Maximum.X, size.X);
                Minimum.X = Math.Min(Minimum.X, size.X);
            }

            if ((double)size.Y > 0.0)
            {
                Maximum.Y = Math.Max(Maximum.Y, size.Y);
                Minimum.Y = Math.Min(Minimum.Y, size.Y);
            }
        }

        Mean = new UnitPoint
        {
            X = Unit.FromMillimeter(sizes.Average(size => size.X.Millimeter)),
            Y = Unit.FromMillimeter(sizes.Average(size => size.Y.Millimeter)),
        };
        Median = new UnitPoint
        {
            X = Unit.FromMillimeter(sizes.Select(size => size.X.Millimeter).Order().ElementAt(sizes.Count / 2)),
            Y = Unit.FromMillimeter(sizes.Select(size => size.Y.Millimeter).Order().ElementAt(sizes.Count / 2)),
        };
    }

    /// <summary>The eight figures, a line each. Original: <c>MuestraDatosEstadisticos</c>.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        foreach ((string name, UnitPoint size) in new[]
        {
            ("Max", Maximum), ("Min", Minimum), (StringResources.Get("Media"), Mean), (StringResources.Get("Mediana"), Median),
        })
        {
            text.AppendLine(name + " X:" + size.X.ToDisplayString());
            text.AppendLine(name + " Y:" + size.Y.ToDisplayString());
        }

        return text.ToString();
    }
}
