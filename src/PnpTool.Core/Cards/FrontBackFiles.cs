using System.Text.RegularExpressions;
using PnpTool.Core.Text;

namespace PnpTool.Core.Cards;

/// <summary>
/// Turns the image files chosen as fronts and as backs into the front and the back of each
/// card to add, for the four ways the add-images window offers.
/// Original: <c>FormSubirImagenesModelo</c>.
/// </summary>
public static partial class FrontBackFiles
{
    /// <summary>Copies in a file name above which the user is warned. Original: the 50 of <c>RevisaNumeroCopiasImagenesASubir</c>.</summary>
    public const int MostCopies = 50;

    /// <summary>
    /// Whether several files can be chosen as fronts and as backs. Pairs are chosen in one
    /// go, so the answer for backs does not matter there. Original: <c>DevolverMultiSelect</c>.
    /// </summary>
    public static (bool Fronts, bool Backs) MultiSelect(AddImagesResult choice) => choice switch
    {
        AddImagesResult.OneFrontManyBacks => (false, true),
        AddImagesResult.ManyFrontsManyBacks or AddImagesResult.OneFrontOneBack => (true, true),
        AddImagesResult.ManyFrontsOneBack => (true, false),
        _ => (false, false),
    };

    /// <summary>Whether a second set of files, the backs, has to be chosen.</summary>
    public static bool AsksForBacks(AddImagesResult choice) => choice != AddImagesResult.OneFrontOneBack;

    /// <summary>
    /// By name without regard to case, numbers by their value ("2" before "10").
    /// Original: <c>FuncionesGenerales.OrdenaAlfabeticamente</c>.
    /// </summary>
    public static string[] SortByName(IEnumerable<string> files) =>
        [.. files.OrderBy(file => Digits().Replace(file, match => match.Value.PadLeft(10, '0')), StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The front and the back of every card, in order. An empty back means the card has none.
    /// <list type="bullet">
    /// <item>Pairs: the files of <paramref name="fronts"/> are taken two by two, front and
    /// back; an odd file out is a front without back.</item>
    /// <item>Many fronts, one back (and the reverse): the one is repeated for each of the many.</item>
    /// <item>Many and many: every front with every back, back by back.</item>
    /// </list>
    /// Original: <c>GeneraRutasFinales</c>.
    /// </summary>
    public static (string[] Fronts, string[] Backs) Pair(IEnumerable<string> fronts, IEnumerable<string> backs, AddImagesResult choice)
    {
        string[] front = SortByName(fronts);
        string[] back = SortByName(backs);
        if (front.Length == 0 || (AsksForBacks(choice) && back.Length == 0))
        {
            return ([], []);
        }

        switch (choice)
        {
            case AddImagesResult.OneFrontOneBack:
                return (
                    [.. front.Where((_, index) => index % 2 == 0)],
                    [.. front.Where((_, index) => index % 2 == 1).Concat(front.Length % 2 == 1 ? [string.Empty] : [])]);
            case AddImagesResult.ManyFrontsOneBack:
                Array.Sort(front, new NumericComparer());
                return (front, [.. Enumerable.Repeat(back[0], front.Length)]);
            case AddImagesResult.OneFrontManyBacks:
                Array.Sort(back, new NumericComparer());
                return ([.. Enumerable.Repeat(front[0], back.Length)], back);
            case AddImagesResult.ManyFrontsManyBacks:
                // The original went past the end of the backs when there were more fronts
                // than backs; with as many backs or more it gave this.
                Array.Sort(back, new NumericComparer());
                return (
                    [.. back.SelectMany(_ => front)],
                    [.. back.SelectMany(one => Enumerable.Repeat(one, front.Length))]);
            default:
                return ([], []);
        }
    }

    /// <summary>
    /// The files whose name asks for more than <see cref="MostCopies"/> copies ("dragon x80.png"),
    /// so the user can be told. Original: <c>FormPrincipalModelo.RevisaNumeroCopiasImagenesASubir</c>,
    /// which named only the last one.
    /// </summary>
    public static string[] WithTooManyCopies(IEnumerable<string> fronts, IEnumerable<string> backs) =>
        [.. fronts.Concat(backs).Where(file => file.Length != 0 && Card.CopiesFromFileName(file) > MostCopies).Distinct()];

    [GeneratedRegex("[0-9]+")]
    private static partial Regex Digits();
}
