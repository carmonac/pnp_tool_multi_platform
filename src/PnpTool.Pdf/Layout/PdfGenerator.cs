using System.Text;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Imaging;

namespace PnpTool.Pdf.Layout;

/// <summary>
/// Makes the PDF of a project from start to finish, with no user interface involved.
/// Original: the steps spread over <c>FormPrincipalModeloLocal.BotonCrearFicheroSalida</c>,
/// <c>FuncionesGenerales.Warnings_GenerarPDF</c> and <c>Window_BarraProgreso</c>.
/// </summary>
public static class PdfGenerator
{
    /// <summary>
    /// The project as the PDF is made from it: a copy whose settings have the sheet turned
    /// when the paper is marked as landscape, and the project type set to gutterfold when
    /// the layout is one (single-sided with backs). The project given is not changed. Pass
    /// the result to <see cref="Prepare"/>, <see cref="Warnings"/> and <see cref="Generate"/>.
    /// Original: the copy <c>ConfAct</c> made in <c>Window_Principal_v2.GenerarFicheroSalida</c>.
    /// </summary>
    /// <remarks>
    /// Kept from the original: the paper's width and height are swapped whenever it is marked
    /// as landscape, whatever they are. Paper stored upright, as the paper control stores it,
    /// comes out landscape; paper already stored wider than tall comes out upright. The
    /// gutterfold style of version 20.11.1 was stored that second way, so on Windows its
    /// four columns do not fit the sheet; the style bundled here is stored upright.
    /// </remarks>
    public static Project ForOutput(Project project)
    {
        var output = new Project { HasChanged = project.HasChanged };
        output.CopyFrom(project);
        PrintLayout print = output.Settings.PrintLayout;
        if (print.Paper.Landscape)
        {
            (print.Paper.Size.X, print.Paper.Size.Y) = (print.Paper.Size.Y, print.Paper.Size.X);
        }

        if (!print.DoubleSided && print.BackImage != BackImageMode.NoBack)
        {
            output.Settings.Group.ProjectType = ProjectType.Gutterfold;
        }

        return output;
    }

    /// <summary>
    /// Builds the list of cells of the project's PDF. With <paramref name="preview"/> only the
    /// first sheet (two with double panels) is kept.
    /// </summary>
    public static PdfImage[] Prepare(Project project, bool preview = false)
    {
        ProjectSettings settings = project.Settings;
        if (settings.Group.ProjectType == ProjectType.AdvancedStyles)
        {
            settings.PrintLayout.SetCardsPerSheet(settings.PrintLayout.Columns);
        }
        else
        {
            settings.PrintLayout.UpdateCardsPerSheet();
        }

        int perSheet = settings.PrintLayout.CardsPerSheet;
        if (settings.DoublePanel)
        {
            perSheet *= 2;
        }

        PdfImage[] images = PdfImageList.Generate(project);
        if (preview && perSheet < images.Length)
        {
            Array.Resize(ref images, perSheet);
        }

        return images;
    }

    /// <summary>
    /// What the user should know before the PDF is made: the size cards will have after the
    /// bleed is cut off, dice that do not have all their faces, images given the project's
    /// colour profile, and images in the wrong colour space. Finding images without the
    /// project's profile also gives them a copy that has it. Original: <c>Warnings_GenerarPDF</c>,
    /// without the checks for the printing company.
    /// </summary>
    public static AppError Warnings(ProjectSettings settings, PdfImage[] images, Deck deck)
    {
        var warning = new AppError();
        var text = new StringBuilder();
        if (settings.Grid.Bleed.Millimeter > 0.0 && settings.Group.Group != SettingsGroup.AdvancedStyles)
        {
            warning.Kind = ErrorKind.Warning;
            text.AppendLine(StringResources.Get("Form_Setup_Disposicion_AvistoTamano"));
            text.AppendLine("  " + StringResources.Get("Ancho") + ": " + Math.Round(settings.Grid.CardSize.X.Millimeter - (2.0 * settings.Grid.Bleed.Millimeter), 2) + AppSettings.MeasurementSuffix);
            text.AppendLine("  " + StringResources.Get("Alto") + ": " + Math.Round(settings.Grid.CardSize.Y.Millimeter - (2.0 * settings.Grid.Bleed.Millimeter), 2) + AppSettings.MeasurementSuffix);
            text.AppendLine(" ");
        }

        if (settings.Group.Group == SettingsGroup.Dice)
        {
            int extra = deck.TotalCopies % 6;
            if (extra != 0)
            {
                warning.Kind = ErrorKind.Warning;
                text.AppendLine("D6=>" + deck.TotalCopies + " = " + (deck.TotalCopies / 6) + " Dices +" + extra + " Images");
            }
        }

        if (settings.Other.ColorProfile is { } profile)
        {
            AppError profiles = ColorManagement.ApplyProjectProfile(deck, profile, settings.DoublePanel, settings.Group.PDFX);
            if (profiles.HasError)
            {
                warning.Kind = ErrorKind.Warning;
                text.AppendLine(profiles.Message);
            }
        }

        AppError colorSpaces = ColorSpaceMismatch(images, settings.Other.ColorSpace, settings.Other.ColorProfile?.Description);
        if (colorSpaces.HasError)
        {
            warning.Kind = ErrorKind.Warning;
            text.AppendLine(colorSpaces.Message);
        }

        if (warning.Kind == ErrorKind.Warning)
        {
            warning.Message = text.ToString();
        }

        return warning;
    }

    /// <summary>
    /// Lists the images whose colour space is not the project's: CMYK images in an RGB project
    /// and the other way round. Original: <c>DisparidadCMYK_RGB</c>.
    /// </summary>
    public static AppError ColorSpaceMismatch(PdfImage[] images, ProjectColorSpace colorSpace, string? profileName)
    {
        var error = new AppError();
        var list = new StringBuilder();
        list.AppendLine(colorSpace == ProjectColorSpace.RGB
            ? StringResources.Get("Ficheros_CMYK_EnRGB")
            : StringResources.Get("Ficheros_RGB_EnCMYK") + " " + profileName);
        for (int i = 0; i < images.Length; i++)
        {
            PdfImage image = images[i];
            if (image is not { Kind: ImageKind.Normal })
            {
                continue;
            }

            bool wrong = colorSpace == ProjectColorSpace.RGB ? image.IsCmyk : !image.IsCmyk;
            if (wrong)
            {
                error.Kind = ErrorKind.Error;
                list.AppendLine("#" + i + "->" + image.Path);
            }
        }

        if (error.HasError)
        {
            error.Message = list.ToString();
        }

        return error;
    }

    /// <summary>
    /// Writes the PDF named in the project's settings (<c>InputParameters.OutputPdf</c>).
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="workFolder">
    /// Folder for the intermediate files, one per sheet; any PDF in it is deleted first. By
    /// default the program's own scratch folder. It must not be the folder of the output.
    /// </param>
    /// <param name="progress">Receives the percentage done and a message.</param>
    /// <param name="preview">Write only the first sheet.</param>
    public static PdfOutput Generate(Project project, string? workFolder = null, Action<int, string>? progress = null, bool preview = false)
    {
        PdfImage[] images = Prepare(project, preview);
        string output = project.Settings.InputParameters.OutputPdf;
        var writer = new PdfLayoutWriter(images, output, project.Settings, workFolder ?? AppPaths.AppTempFolder.TrimEnd(Path.DirectorySeparatorChar), project.AdvancedTemplate)
        {
            ReportProgress = progress,
        };
        return writer.Create();
    }
}
