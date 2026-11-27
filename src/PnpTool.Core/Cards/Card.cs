using System.Xml.Serialization;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Cards;

/// <summary>A card: front, back and number of copies. Original: <c>Carta</c>.</summary>
[XmlType("Carta")]
public class Card
{
    [XmlElement("CaraTrasera")]
    public CardFace Back { get; set; }

    [XmlElement("CaraFrontal")]
    public CardFace Front { get; set; }

    [XmlElement("NumerodeCopias")]
    public int Copies { get; set; }

    [XmlIgnore]
    public bool Selected { get; set; }

    public Card()
    {
        Copies = 0;
        Front = new CardFace();
        Back = new CardFace();
        Selected = false;
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(Card source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Front.CopyFrom(source.Front);
        Back.CopyFrom(source.Back);
        Copies = source.Copies;
        Selected = source.Selected;
    }

    /// <summary>
    /// Builds both faces from files, reading each image's properties. Original: <c>CartaDesdeArchivos</c>.
    /// </summary>
    public void LoadFromFiles(string frontPath, ImageKind frontKind, string backPath, ImageKind backKind)
    {
        if (frontKind == ImageKind.Normal)
        {
            Front = ImageInspector.Current.GetImageProperties(frontPath);
        }

        Front.FaceType = FaceType.Front;
        Front.ImageKind = frontKind;
        if (backKind == ImageKind.Normal)
        {
            Back = ImageInspector.Current.GetImageProperties(backPath);
        }

        Back.FaceType = FaceType.Back;
        Back.ImageKind = backKind;
        Copies = Front.ImageKind == ImageKind.Normal ? CopiesFromFileName(frontPath) : 1;
    }

    /// <summary>
    /// What to tell the user when some of the cards have an image in another colour space
    /// than the project's, or null when there is nothing to tell: none has, or the user
    /// turned the warning off and it was already given once since the program started.
    /// Original: <c>FormPrincipalModeloLocal.IncongruenciaEspacioColorCartaProyecto</c>.
    /// </summary>
    /// <remarks>
    /// The original looked only at the first card of those added, and counted the warning as
    /// given even when that card was fine, so with the warning turned off (as it comes) only
    /// the first image added after starting the program could ever raise it.
    /// </remarks>
    public static string? ColorSpaceWarning(IEnumerable<Card> cards, ProjectColorSpace projectColorSpace)
    {
        if (!AppSettings.ShowCmykWarning && !AppSettings.ShowCmykWarningOnce)
        {
            return null;
        }

        bool wantsCmyk = projectColorSpace == ProjectColorSpace.CMYK;
        static bool Differs(CardFace face, bool wantsCmyk) => face.ImageKind == ImageKind.Normal && !face.IsCorrupt && face.IsCmyk != wantsCmyk;
        if (!cards.Any(card => Differs(card.Front, wantsCmyk) || Differs(card.Back, wantsCmyk)))
        {
            return null;
        }

        AppSettings.ShowCmykWarningOnce = false;
        return wantsCmyk
            ? StringResources.Get("Ficheros_RGB_EnCMYK") + " CMYK"
            : StringResources.Get("Ficheros_CMYK_EnRGB");
    }

    /// <summary>
    /// Number of copies encoded at the end of a file name: "dragon x3.png" or "dragonX12.jpg"
    /// give 3 and 12; anything else gives 1, and an empty name 0.
    /// Original: <c>ImagenesGeneral.DevuelveNumeroCopias</c>.
    /// </summary>
    public static int CopiesFromFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return 0;
        }

        string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        if (name.Length == 0)
        {
            return 0;
        }

        // Walk back over the trailing digits; the character before them must be an x.
        int index = name.Length;
        do
        {
            index--;
        }
        while (char.IsDigit(name[index]) && index > 0);

        if (index + 1 == name.Length)
        {
            return 1;
        }

        if (char.ToLower(name[index]) != 'x')
        {
            return 1;
        }

        string digits = name[(index + 1)..];
        if (digits == string.Empty)
        {
            return 1;
        }

        return int.TryParse(digits, out int copies) ? copies : 1;
    }

    /// <summary>Deletes the face's working file when it is a temporary copy. Original: <c>BorraFicherosRutaCara</c>.</summary>
    public static void DeleteTempFile(CardFace face)
    {
        if (System.IO.Path.GetDirectoryName(face.Path) + System.IO.Path.DirectorySeparatorChar == AppSettings.TempFolder
            && face.OriginalPath != face.Path
            && File.Exists(face.Path))
        {
            File.Delete(face.Path);
        }
    }

    /// <summary>Original: <c>BorraFicherosRuta</c>.</summary>
    public void DeleteTempFiles()
    {
        DeleteTempFile(Front);
        DeleteTempFile(Back);
    }
}
