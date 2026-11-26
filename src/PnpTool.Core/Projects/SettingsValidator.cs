using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>
/// The checks made on the settings of a project before its PDF is made.
/// Original: <c>FormSetupModelo.CompruebaConfiguracion</c>.
/// </summary>
/// <remarks>
/// Everything found wrong is reported, a line each. The checks expect the paper as the PDF will use it, that is, after
/// <c>PdfGenerator.ForOutput</c> has turned a sheet marked as landscape; the original checked
/// the paper as stored and then turned it, so it could accept a layout that did not fit the
/// sheet actually written.
/// </remarks>
public static class SettingsValidator
{
    /// <summary>
    /// What is wrong with the settings, as an error; an error of kind none when nothing is.
    /// A layout with fronts and backs on one side is marked as a gutterfold project, as the
    /// original does here.
    /// </summary>
    /// <param name="settings">The settings, with the paper turned for output.</param>
    /// <param name="cardCount">How many cards the project has, copies counted.</param>
    public static AppError Check(ProjectSettings settings, int cardCount)
    {
        var messages = new List<string>();
        Gutterfold(settings, messages);
        RowsAndColumns(settings, messages);
        Dimensions(settings, messages);
        FaceSizes(settings, messages);
        MissingBack(settings, messages);
        if (settings.Watermark.Type != WatermarkType.None && string.IsNullOrEmpty(settings.Watermark.Path))
        {
            messages.Add(StringResources.Get("imagen_marca_agua_vacia") + " ");
        }

        if (settings.Group.ProjectType == ProjectType.SpanishDeck)
        {
            // Original: CompruebaConfiguracion_BarajaEspanola; the second and third styles of the group are the two decks with masks.
            if (settings.Group.SubgroupIndex == 1 && cardCount != 40)
            {
                messages.Add(StringResources.Get("Baraja_Espanola_No40"));
            }

            if (settings.Group.SubgroupIndex == 2 && cardCount != 52)
            {
                messages.Add(StringResources.Get("Baraja_Poker_No52"));
            }
        }

        bool mirror = settings.Front.Effect == ImageEffectType.Mirror || settings.Back.Effect == ImageEffectType.Mirror;
        if (mirror && !(settings.Grid.Bleed.Point > 0.0))
        {
            messages.Add(StringResources.Get("Sangrado_no_puede_ser_0"));
        }

        MirrorBleed(settings, messages);
        OuterFrames(settings, messages);
        var error = new AppError();
        foreach (string message in messages)
        {
            error.Message = message;
            error.Kind = ErrorKind.Error;
        }

        return error;
    }

    /// <summary>Which faces get a background painted. Original: <c>CambiaConfiguracionFinalRelleno</c>.</summary>
    public static FaceType FillFaces(ProjectSettings settings) =>
        (settings.Front.PaintBackground, settings.Back.PaintBackground) switch
        {
            (true, true) => FaceType.Both,
            (true, false) => FaceType.Front,
            (false, true) => FaceType.Back,
            _ => FaceType.None,
        };

    /// <summary>
    /// An error naming the last card whose front image file is not there, or of kind none.
    /// Original: <c>FuncionesGenerales.ExistenTodosLosFicheros</c>.
    /// </summary>
    public static AppError MissingFiles(Card[] cards)
    {
        var error = new AppError();
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i].Front.ImageKind == ImageKind.Normal && !File.Exists(cards[i].Front.Path))
            {
                error = new AppError { Kind = ErrorKind.Error, Message = "#" + i + " Path not found: " + cards[i].Front.Path };
            }
        }

        return error;
    }

    /// <summary>Original: <c>CompruebaConfiguracion_Glutter</c>.</summary>
    private static void Gutterfold(ProjectSettings settings, List<string> messages)
    {
        PrintLayout print = settings.PrintLayout;
        var found = new List<string>();
        if (!print.DoubleSided && print.BackImage != BackImageMode.NoBack)
        {
            settings.Group.ProjectType = ProjectType.Gutterfold;
            if (print.Rows % 2 != 0)
            {
                found.Add(StringResources.Get("Gutter_paginas_par"));
            }
        }

        string line = StringResources.Get("Form_Setup_Caras_CaraFrontal_LabelLineaCorteFrontal") + " " + StringResources.Get("grosor") + "<=0";
        if (settings.Front.CutLine.LineType != CutLineType.None && (double)settings.Front.CutLine.Thickness <= 0.0)
        {
            found.Add(StringResources.Get("Frontal") + " " + line);
        }

        if (settings.Back.CutLine.LineType != CutLineType.None && (double)settings.Back.CutLine.Thickness <= 0.0)
        {
            found.Add(StringResources.Get("Trasera") + " " + line);
        }

        messages.AddRange(found);
    }

    /// <summary>Original: <c>CompruebaConfiguracion_FilasColumnas</c>.</summary>
    private static void RowsAndColumns(ProjectSettings settings, List<string> messages)
    {
        if (settings.Group.ProjectType == ProjectType.AdvancedStyles)
        {
            return;
        }

        if (settings.PrintLayout.Columns == 0)
        {
            messages.Add(StringResources.Get("Columnas") + "=0");
        }

        if (settings.PrintLayout.Rows == 0)
        {
            messages.Add(StringResources.Get("Filas") + "=0");
        }
    }

    /// <summary>Original: <c>CompruebaConfiguracion_Dimensiones</c>.</summary>
    private static void Dimensions(ProjectSettings settings, List<string> messages)
    {
        if (settings.Group.Group == SettingsGroup.AdvancedStyles)
        {
            return;
        }

        Grid grid = settings.Grid;
        PrintLayout print = settings.PrintLayout;
        UnitPoint paper = print.Paper.Size;
        var found = new List<string>();
        string images = StringResources.Get("Form_Setup_GroupBox_Imagenes") + " " + StringResources.Get("Tamano");
        if (grid.CardSize.X.Millimeter <= 0.0)
        {
            found.Add(images + " X <=0");
        }

        if (grid.CardSize.Y.Millimeter <= 0.0)
        {
            found.Add(images + " Y <=0");
        }

        Unit totalX = Unit.FromMillimeter((grid.CardSize.X.Millimeter * print.Columns) + (grid.Spacing.X.Millimeter * (print.Columns - 1)));
        Unit totalY = Unit.FromMillimeter((grid.CardSize.Y.Millimeter * print.Rows) + (grid.Spacing.Y.Millimeter * (print.Rows - 1)));
        if (!((double)totalX <= (double)paper.X && (double)totalY <= (double)paper.Y))
        {
            found.Add(StringResources.Get("Cartas_no_entran_ancho") + " " + totalX.ToDisplayString() + ">=" + paper.X.ToDisplayString()
                + " OR " + totalY.ToDisplayString() + ">=" + paper.Y.ToDisplayString());
        }

        if ((double)grid.Bleed > (double)grid.CardSize.X)
        {
            found.Add(StringResources.Get("Form_Setup_LabelSangrado") + ">" + StringResources.Get("Ancho"));
        }

        if ((double)grid.Bleed > (double)grid.CardSize.Y)
        {
            found.Add(StringResources.Get("Form_Setup_LabelSangrado") + ">" + StringResources.Get("Alto"));
        }

        messages.AddRange(found);
    }

    /// <summary>Original: <c>CompruebaConfiguracion_Tamanos</c>.</summary>
    private static void FaceSizes(ProjectSettings settings, List<string> messages)
    {
        UnitPoint card = settings.Grid.CardSize;
        string maxX = " MAX X:" + card.X.ToDisplayString();
        string maxY = " MAX Y:" + card.Y.ToDisplayString();
        if (Greater(settings.Front.Size.X, card.X))
        {
            messages.Add(StringResources.Get("Carta_Escala_Ancho_Frontal_Error") + " Es: " + settings.Front.Size.X.ToDisplayString() + maxX);
        }

        if (Greater(settings.Front.Size.Y, card.Y))
        {
            messages.Add(StringResources.Get("Carta_Escala_Alto_Frontal_Error") + " Es: " + settings.Front.Size.Y.ToDisplayString() + maxY);
        }

        if (!settings.PrintLayout.DoubleSided)
        {
            return;
        }

        if (Greater(settings.Back.Size.X, card.X))
        {
            messages.Add(StringResources.Get("Carta_Escala_Ancho_Trasera_Error") + " Es: " + settings.Back.Size.X.ToDisplayString() + maxX);
        }

        if (Greater(settings.Back.Size.Y, card.Y))
        {
            // The original reports the width here too.
            messages.Add(StringResources.Get("Carta_Escala_Alto_Trasera_Error") + " Es: " + settings.Back.Size.Y.ToDisplayString() + maxY);
        }
    }

    /// <summary>Original: <c>CompruebaConfiguracion_FaltaTrasera</c>.</summary>
    private static void MissingBack(ProjectSettings settings, List<string> messages)
    {
        if (settings.PrintLayout.DoubleSided
            && settings.Group.ProjectType != ProjectType.Papermini
            && string.IsNullOrEmpty(settings.SharedBack.Path)
            && settings.PrintLayout.BackImage is BackImageMode.SameBack or BackImageMode.OnlyOne or BackImageMode.Pattern)
        {
            messages.Add(StringResources.Get("falta_imagen_trasera"));
        }
    }

    /// <summary>
    /// With bleed, a face with the mirror effect must measure the card less the bleed on each
    /// side. Original: <c>ErrorEnConfiguracionSangrado</c>.
    /// </summary>
    private static void MirrorBleed(ProjectSettings settings, List<string> messages)
    {
        Unit bleed = settings.Grid.Bleed;
        if (bleed.Point == 0.0)
        {
            return;
        }

        var found = new List<string>();
        UnitPoint card = settings.Grid.CardSize;
        foreach (FaceSettings face in new[] { settings.Front, settings.Back })
        {
            if (face.Effect != ImageEffectType.Mirror)
            {
                continue;
            }

            foreach ((Unit cardSide, Unit faceSide) in new[] { (card.X, face.Size.X), (card.Y, face.Size.Y) })
            {
                if (!cardSide.EqualsInMillimeters(Unit.FromPoint((double)faceSide + (2.0 * (double)bleed))))
                {
                    Unit expected = Unit.FromPoint((double)cardSide - (2.0 * (double)bleed));
                    found.Add(faceSide.ToDisplayString() + "=>" + expected.ToDisplayString());
                }
            }
        }

        messages.AddRange(found);
    }

    /// <summary>Original: <c>CompruebaConfiguracion_MarcoExterior</c>.</summary>
    private static void OuterFrames(ProjectSettings settings, List<string> messages)
    {
        foreach ((FaceSettings face, string name) in new[] { (settings.Front, "Frontal"), (settings.Back, "Trasera") })
        {
            if (!face.ShowOuterFrame)
            {
                continue;
            }

            if ((double)face.OuterFrameSize <= 0.0)
            {
                messages.Add(StringResources.Get(name) + " " + StringResources.Get("Form_Setup_Caras_CaraFrontal_CheckboxMarcoExterior") + "<=0");
            }

            if (face.OuterFrameEffect == OuterFrameEffect.Mirror && !settings.Grid.CardSize.EqualsInMillimeters(face.Size))
            {
                messages.Add(StringResources.Get(name) + " " + StringResources.Get("Efecto_Espejo_MismoTamano"));
            }
        }
    }

    private static bool Greater(Unit a, Unit b) => Math.Round(a.Millimeter, 2) > Math.Round(b.Millimeter, 2);
}
