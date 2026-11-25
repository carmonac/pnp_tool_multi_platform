using ImageMagick;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Layout.Element;
using iText.Layout.Properties;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;
using PnpTool.Imaging;
using Path = System.IO.Path;
using PointD = PnpTool.Core.Primitives.PointD;
using TextAlignment = iText.Layout.Properties.TextAlignment;

namespace PnpTool.Pdf.Layout;

/// <content>
/// What is drawn on a sheet besides the cards: outer frames, calibration marks and image,
/// footer text, cut lines, perforation marks, the file ID and the watermark over the page.
/// Original: the drawing methods of <c>FicheroPdfMaquetadoItext</c>.
/// </content>
public partial class PdfLayoutWriter
{
    /// <summary>A stored colour as a PDF colour of the project's kind. Original: <c>GestionDelColor.DevuelveItextColor</c>.</summary>
    private Color PdfColor(string color)
    {
        ArgbColor argb = ArgbColor.Parse(color);
        if (!_cmykProject)
        {
            return new DeviceRgb(argb.R, argb.G, argb.B);
        }

        (byte c, byte m, byte y, byte k) = argb.ToCmykPercent();
        return new DeviceCmyk(c, m, y, k);
    }

    private FaceSettings CurrentFaceSettings => _layout.CurrentFace == FaceType.Back ? _back : _front;

    // ------------------------------------------------------------ footer

    /// <summary>
    /// Drawn when a sheet starts, under the cards: the outer frame, the four calibration marks
    /// and the signature and page number. Original: <c>DibujaPiePagina</c>.
    /// </summary>
    partial void DrawFooter(int pageNumber, FaceType face, bool overrideEmpty, PdfImage[]? images)
    {
        var printable = new UnitPoint(
            (double)_print.Paper.Size.X - (2.0 * (double)_layout.SafetyMargin.X),
            (double)_print.Paper.Size.Y - (2.0 * (double)_layout.SafetyMargin.Y));
        if (_layout.HasOuterFrame(face))
        {
            switch (_layout.OuterFrameEffectFor(face))
            {
                case OuterFrameEffect.SolidColor:
                    if (_group.ProjectType != ProjectType.AdvancedStyles)
                    {
                        DrawSolidFrames(overrideEmpty);
                    }
                    else
                    {
                        DrawSolidFramesForTemplate(overrideEmpty);
                    }

                    break;
                case OuterFrameEffect.Mirror:
                    DrawMirrorFrame(images);
                    break;
            }
        }

        if (_settings.HeaderFooter.ShowCalibrationMark)
        {
            DrawCalibrationMarks(printable);
        }

        string text = _layout.FooterText(face, pageNumber);
        if (!string.IsNullOrEmpty(text))
        {
            DrawFooterText(printable, text, evenPage: pageNumber % 2 == 0);
        }
    }

    /// <summary>A target mark near each corner of the sheet. Original: <c>DibujaPiePaginaMarcaCalibracion</c>.</summary>
    private void DrawCalibrationMarks(UnitPoint printable)
    {
        HeaderFooter header = _settings.HeaderFooter;
        double size = header.CalibrationMarkSize.Point;
        double offsetX = header.CalibrationMarkPosition.X.Point;
        double offsetY = header.CalibrationMarkPosition.Y.Point;
        double marginX = _layout.SafetyMargin.X.Point;
        double marginY = _layout.SafetyMargin.Y.Point;
        ImageData mark = StockImageData(StockImage.CalibrationMark, _other, _group.PDFX);

        float left = (float)(marginX + offsetX);
        float bottom = (float)(marginY + offsetY);
        float right = (float)(printable.X.Point - size + marginX - offsetX);
        float top = (float)(printable.Y.Point - size + marginY - offsetY);
        foreach ((float x, float y) in new[] { (left, bottom), (right, bottom), (right, top), (left, top) })
        {
            _document!.Add(new Image(mark).ScaleAbsolute((float)size, (float)size).SetFixedPosition(1, x, y));
        }
    }

    /// <summary>
    /// Page numbers at the left or right alternate sides on odd and even pages, as in a book.
    /// Original: <c>DevuelveAlineacion</c>.
    /// </summary>
    private TextAlignment FooterAlignment(bool evenPage) => _settings.HeaderFooter.PageNumberPosition switch
    {
        PageNumberPosition.Left => evenPage ? TextAlignment.LEFT : TextAlignment.RIGHT,
        PageNumberPosition.Right => evenPage ? TextAlignment.RIGHT : TextAlignment.LEFT,
        _ => TextAlignment.CENTER,
    };

    /// <summary>Original: <c>DibujaPiePaginaTextoAbajo</c>.</summary>
    private void DrawFooterText(UnitPoint printable, string text, bool evenPage)
    {
        var paragraph = new Paragraph();
        if (_font != null)
        {
            paragraph.SetFont(_font);
        }

        paragraph.SetFontSize(_settings.Font.Size);
        float x = (float)_layout.SafetyMargin.X.Point;
        float y = (float)(double)Unit.FromMillimeter((int)Math.Round(Unit.FromPoint(_settings.Font.Height).Millimeter, 0));
        paragraph.SetTextAlignment(FooterAlignment(evenPage));
        paragraph.Add(_layout.FitFooterText(text, printable));
        paragraph.SetFixedPosition(x, y, (float)printable.X.Point);
        _canvas!.SaveState();
        _canvas.SetFillColor(PdfColor(AppSettings.ColorBlack));
        _document!.Add(paragraph);
        _canvas.RestoreState();
    }

    // ------------------------------------------------------------ outer frames

    /// <summary>Size of one card with its frame, and the frame's width. Original: <c>EstableceTamanoMarco</c>.</summary>
    private (float Width, float Height, Unit Frame) FrameSize()
    {
        Unit frame = CurrentFaceSettings.OuterFrameSize;
        return ((float)((double)_grid.CardSize.X + (2.0 * (double)frame)), (float)((double)_grid.CardSize.Y + (2.0 * (double)frame)), frame);
    }

    private ImageData SolidColor(string color, string pathForFormat) =>
        ToImageData(SolidColorImages.Create(color, _other.ColorProfile, _other.ColorSpace, ImageFormats.FormatFromExtension(Path.GetExtension(pathForFormat))));

    /// <summary>
    /// A coloured rectangle behind every card of the sheet, larger than the card by the frame
    /// width. Original: <c>DibujaMarcoExterior_ColorSolido_Normal</c>.
    /// </summary>
    private void DrawSolidFrames(bool overrideEmpty)
    {
        FaceSettings face = CurrentFaceSettings;
        double shiftX = ((double)_grid.CardSize.X - (double)face.Size.X) / 2.0;
        double shiftY = ((double)_grid.CardSize.Y - (double)face.Size.Y) / 2.0;
        (float width, float height, Unit frame) = FrameSize();
        _canvas!.SaveState();
        _layout.Cell.X = 0;
        _layout.Cell.Y = 0;
        int perSheet = _print.CardsPerSheet;
        for (int row = 0; row < _print.Rows; row++)
        {
            for (int column = 0; column < _print.Columns; column++)
            {
                int index = (row * _print.Columns) + column + (_layout.CurrentSheet * perSheet);
                PdfImage? image = index < _layout.Images.Length ? _layout.Images[index] : null;
                if (index < _totalImages && image != null && (overrideEmpty || image.Kind != ImageKind.Empty))
                {
                    _layout.SetPositionForCurrentCell();
                    float x = (float)(_layout.Position.X.Point - frame.Point - shiftX);
                    float y = (float)(_layout.Position.Y.Point - frame.Point - shiftY);
                    _canvas.AddImageFittedIntoRectangle(SolidColor(face.OuterFrameColor, image.Path), new Rectangle(x, y, width, height), false);
                }

                _layout.Advance();
            }
        }

        _layout.Cell.X = 0;
        _layout.Cell.Y = 0;
        _canvas.RestoreState();
    }

    /// <summary>The same for the places of an advanced template. Original: <c>DibujaMarcoExterior_ColorSolido_EstiloAvanzado</c>.</summary>
    private void DrawSolidFramesForTemplate(bool overrideEmpty)
    {
        FaceSettings face = CurrentFaceSettings;
        double shiftX = ((double)_grid.CardSize.X - (double)face.Size.X) / 2.0;
        double shiftY = ((double)_grid.CardSize.Y - (double)face.Size.Y) / 2.0;
        Unit frame = face.OuterFrameSize;
        _canvas!.SaveState();
        AdvancedTemplateCard[] places = _layout.Template.Cards;
        for (int i = 0; i < places.Length && i < _layout.Images.Length; i++)
        {
            PdfImage image = _layout.Images[i];
            if (overrideEmpty || image.Kind != ImageKind.Empty)
            {
                float width = (float)(places[i].Size.X.Point + (2.0 * (double)frame));
                float height = (float)(places[i].Size.Y.Point + (2.0 * (double)frame));
                _layout.SetPositionForCurrentCell();
                float x = (float)(_layout.Position.X.Point - frame.Point - shiftX);
                float y = (float)(_layout.Position.Y.Point - frame.Point - shiftY);
                _canvas.AddImageFittedIntoRectangle(SolidColor(face.OuterFrameColor, image.Path), new Rectangle(x, y, width, height), false);
            }

            _layout.Advance();
        }

        _layout.Cell.X = 0;
        _layout.Cell.Y = 0;
        _canvas.RestoreState();
    }

    /// <summary>The cells of the sheet about to be written. Original: <c>DibujaMarcoExterior_Espejo_GeneraListadoImagenes</c>.</summary>
    private PdfImage[] CellsOfCurrentSheet(PdfImage[]? given)
    {
        int perSheet = _print.CardsPerSheet;
        if (_print.BackImage == BackImageMode.SameBack && _layout.CurrentFace == FaceType.Back)
        {
            if (given != null)
            {
                return given;
            }

            var shared = new PdfImage(_settings.SharedBack);
            return [.. Enumerable.Repeat(shared, perSheet)];
        }

        int first = _layout.CurrentSheet * perSheet;
        var cells = new PdfImage[perSheet];
        for (int i = 0; i < perSheet; i++)
        {
            cells[i] = first + i < _layout.Images.Length ? _layout.Images[first + i] : PdfImage.Empty();
        }

        return cells;
    }

    /// <summary>
    /// A frame around the whole grid made of the outermost cards mirrored outwards: the cards
    /// of the sheet are joined into one picture, a mirrored frame is added to it, and the
    /// result is laid under the grid. Original: <c>DibujaMarcoExterior_Espejo</c>, which built
    /// the joined picture with the tabletop generator; here it is put together directly.
    /// </summary>
    private void DrawMirrorFrame(PdfImage[]? given)
    {
        Log.Enter(nameof(DrawMirrorFrame));
        PdfImage[] cells = CellsOfCurrentSheet(given);
        Unit frame = CurrentFaceSettings.OuterFrameSize;
        string file = AppSettings.TempFolder + "ImagenPatron_0_front.png";
        using (MagickImage sheet = JoinCells(cells))
        {
            sheet.Write(file);
        }

        (_, MagickImage framed) = MirrorEffect.AddFrame(file, frame, _other.ResizeFilter, _other.ColorProfile);
        using (framed)
        {
            framed.Write(file);
        }

        float width = (float)((_grid.CardSize.X.Point * _print.Columns) + (2.0 * frame.Point));
        float height = (float)((_grid.CardSize.Y.Point * _print.Rows) + (2.0 * frame.Point));
        ImageData data = ToImageData(_layout.PrepareImage(file, new RotationProperties(), _group.PDFX));
        _layout.Cell.X = 0;
        _layout.Cell.Y = _print.Rows - 1;
        _layout.SetPositionForCurrentCell();
        float x = (float)(_layout.Position.X.Point - frame.Point);
        float y = (float)(_layout.Position.Y.Point - frame.Point);
        _canvas!.AddImageFittedIntoRectangle(data, new Rectangle(x, y, width, height), false);
        _layout.Cell.X = 0;
        _layout.Cell.Y = 0;
        DeleteQuietly(file);
        Log.Exit(nameof(DrawMirrorFrame));
    }

    /// <summary>
    /// The cells of a sheet side by side in one picture, without spacing, at 300 dots per
    /// inch. Empty and missing cells are white; blank cells take their colour.
    /// </summary>
    private MagickImage JoinCells(PdfImage[] cells)
    {
        const int dpi = 300;
        uint cellWidth = (uint)Math.Max(1, Convert.ToInt32(_grid.CardSize.X.Inch * dpi));
        uint cellHeight = (uint)Math.Max(1, Convert.ToInt32(_grid.CardSize.Y.Inch * dpi));
        var sheet = new MagickImage(MagickColors.White, cellWidth * (uint)_print.Columns, cellHeight * (uint)_print.Rows)
        {
            Format = MagickFormat.Png,
            Density = new Density(dpi, DensityUnit.PixelsPerInch),
        };
        var size = new MagickGeometry(cellWidth, cellHeight) { IgnoreAspectRatio = true };
        for (int i = 0; i < cells.Length && i < _print.Rows * _print.Columns; i++)
        {
            PdfImage cell = cells[i];
            MagickImage? picture = cell.Kind switch
            {
                ImageKind.Normal when File.Exists(cell.Path) => ImageTransforms.Rotate(new MagickImage(cell.Path), cell.Rotation),
                ImageKind.Blank => new MagickImage(ArgbColor.Parse(cell.Path).ToMagick(), cellWidth, cellHeight),
                _ => null,
            };
            if (picture == null)
            {
                continue;
            }

            using (picture)
            {
                picture.ResetPage();
                picture.FilterType = _other.ResizeFilter;
                picture.Resize(size);
                sheet.Composite(picture, (int)(cellWidth * (uint)(i % _print.Columns)), (int)(cellHeight * (uint)(i / _print.Columns)), CompositeOperator.Over);
            }
        }

        return sheet;
    }

    // ------------------------------------------------------------ calibration image

    /// <summary>
    /// The calibration strip, centred at the top of the first sheet. Original: <c>InsertaMarcaCalibracionColor</c>.
    /// </summary>
    partial void DrawCalibrationImage()
    {
        HeaderFooter header = _settings.HeaderFooter;
        if (!header.ShowCalibrationImage)
        {
            return;
        }

        float width = (float)header.CalibrationImageSize.X.Point;
        float height = (float)header.CalibrationImageSize.Y.Point;
        float x = (float)((_print.Paper.Size.X.Point / 2.0) - (header.CalibrationImageSize.X.Point / 2.0));
        float y = (float)(_print.Paper.Size.Y.Point - _layout.SafetyMargin.Y.Point - header.CalibrationImageSize.Y.Point);
        Image strip = new Image(StockImageData(StockImage.Calibration, _other, _group.PDFX)).ScaleAbsolute(width, height).SetFixedPosition(1, x, y);
        _document!.Add(strip);
    }

    // ------------------------------------------------------------ cut lines

    private void Line(PointD from, PointD to)
    {
        _canvas!.MoveTo(from.X, from.Y);
        _canvas.LineTo(to.X, to.Y);
        _canvas.ClosePathFillStroke();
    }

    private void Line(LineD line) => Line(line.From, line.To);

    /// <summary>
    /// The cut lines of the sheet in the style chosen for the face. Original: <c>MostrarLineasCorte_V2</c>.
    /// </summary>
    partial void DrawCutLines(FaceType face)
    {
        CutLineSettings cut = face == FaceType.Front ? _front.CutLine : _back.CutLine;
        CutLineType type = cut.LineType;
        if (type == CutLineType.None)
        {
            return;
        }

        // Half the length of a cross arm in millimetres; a "dot" is a cross a quarter of a millimetre long.
        double crossSize = type == CutLineType.Dot ? 0.25 : cut.Gap;
        float thickness = (float)cut.Thickness.Point;
        Color color = PdfColor(cut.Color);
        bool backs = face != FaceType.Front;
        if (backs)
        {
            _layout.GridOrigin.X = (double)_layout.GridOrigin.X + (double)_settings.BackOffset.X;
            _layout.GridOrigin.Y = (double)_layout.GridOrigin.Y - (double)_settings.BackOffset.Y;
        }

        _canvas!.SaveState();
        _canvas.SetLineWidth(thickness);
        _canvas.SetStrokeColor(color);
        _canvas.SetFillColor(color);
        switch (type)
        {
            case CutLineType.Cross:
            case CutLineType.Dot:
                DrawCrosses(cut.CrossLinesToEdge, face, outerOnes: true, crossSize);
                _canvas.RestoreState();
                break;
            case CutLineType.Normal:
                DrawOutsideLines(cut.Gap);
                _canvas.RestoreState();
                break;
            case CutLineType.Full:
                DrawFullLines(cut.Gap);
                _canvas.RestoreState();
                break;
            case CutLineType.Mixed:
                // Lines outside the grid plus thin crosses at the inner corners. The original
                // saved the graphics state twice here and restored it once; both are restored.
                DrawOutsideLines(cut.Gap);
                _canvas.SaveState();
                _canvas.SetLineWidth(thickness / 2f);
                DrawCrosses(toEdge: false, face, outerOnes: false, crossSize);
                _canvas.RestoreState();
                _canvas.RestoreState();
                break;
        }

        if (backs)
        {
            _layout.GridOrigin.X = (double)_layout.GridOrigin.X - (double)_settings.BackOffset.X;
            _layout.GridOrigin.Y = (double)_layout.GridOrigin.Y + (double)_settings.BackOffset.Y;
        }
    }

    private bool IsGutterfoldWithGap => _group.ProjectType == ProjectType.Gutterfold && _grid.Spacing.Y != 0;

    /// <summary>Lines across the whole sheet. Original: <c>MostrarLineasCorte_V3_Completa</c>.</summary>
    private void DrawFullLines(int gap)
    {
        for (int direction = 0; direction < 2; direction++)
        {
            PointD[,] lines = CutLines.Outside(direction != 0, gap, _layout);
            for (int i = 0; i < lines.GetLength(0); i++)
            {
                Line(lines[i, 0], lines[i, 3]);

                // The fold line of a gutterfold, in the middle of the gap between a row of
                // fronts and the row of backs before it.
                if (IsGutterfoldWithGap && i > 0 && i % 2 == 0)
                {
                    double half = _grid.Spacing.Y.Point / 2.0;
                    Line(new PointD(lines[i, 0].X, lines[i, 0].Y - half), new PointD(lines[i, 3].X, lines[i, 3].Y - half));
                }
            }
        }
    }

    /// <summary>Lines from the edges of the sheet that stop before the cards. Original: <c>MostrarLineasCorte_V5_Normal</c>.</summary>
    private void DrawOutsideLines(int gap)
    {
        for (int direction = 0; direction < 2; direction++)
        {
            PointD[,] lines = CutLines.Outside(direction != 0, gap, _layout);
            for (int i = 0; i < lines.GetLength(0); i++)
            {
                Line(lines[i, 0], lines[i, 1]);
                Line(lines[i, 2], lines[i, 3]);
                if (IsGutterfoldWithGap && direction == 0 && i > 0 && i % 2 == 0)
                {
                    double half = _grid.Spacing.Y.Point / 2.0;
                    Line(new PointD(lines[i, 0].X, lines[i, 0].Y - half), new PointD(lines[i, 1].X, lines[i, 1].Y - half));
                    Line(new PointD(lines[i, 2].X, lines[i, 2].Y - half), new PointD(lines[i, 3].X, lines[i, 3].Y - half));
                }
            }
        }
    }

    /// <summary>
    /// A cross at each corner of each card. Without <paramref name="outerOnes"/> the crosses on
    /// the outline of the grid are left out. Original: <c>MostrarLineasCorte_V6_Cruz</c>,
    /// <c>MostrarLineasCorte_V7_Cruz_Normal</c> and <c>MostrarLineasCorte_V5_Cruz_EstilosAvanzados</c>.
    /// </summary>
    private void DrawCrosses(bool toEdge, FaceType face, bool outerOnes, double crossSize)
    {
        if (_group.ProjectType == ProjectType.AdvancedStyles)
        {
            LineD[,,] templateCrosses = CutLines.TemplateCrosses(_layout.Template, face == FaceType.Back ? _layout.BackPositions : _layout.FrontPositions);
            for (int place = 0; place < _layout.Template.Cards.Length; place++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    Line(templateCrosses[place, corner, 0]);
                    Line(templateCrosses[place, corner, 1]);
                }
            }

            return;
        }

        LineD[,,] crosses = CutLines.Crosses(toEdge, _layout, crossSize);
        int firstColumn = 0;
        int lastColumn = _print.Columns * 2;
        int firstRow = 0;
        int lastRow = _print.Rows * 2;
        if (!outerOnes)
        {
            firstColumn++;
            lastColumn--;
            firstRow++;
            lastRow--;
        }

        double foldShift = (_grid.Spacing.Y.Point / 2.0) + _grid.Bleed.Point;
        for (int column = firstColumn; column < lastColumn; column++)
        {
            for (int row = firstRow; row < lastRow; row++)
            {
                Line(crosses[column, row, 0]);
                Line(crosses[column, row, 1]);
                if (IsGutterfoldWithGap && row > 0 && row % 2 == 0)
                {
                    foreach (LineD arm in new[] { crosses[column, row, 0], crosses[column, row, 1] })
                    {
                        arm.From.Y -= foldShift;
                        arm.To.Y -= foldShift;
                        Line(arm);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------ marks, ID, watermark

    /// <summary>
    /// Dots along one side of the sheet showing where to punch holes for binding: on the same
    /// physical edge of every leaf, so on alternate sides of consecutive sheets.
    /// Original: <c>MostrarMarcasPerforacion</c>.
    /// </summary>
    partial void DrawPerforationMarks()
    {
        PerforationMarks marks = _settings.Book.PerforationMarks;
        UnitPoint paper = _print.Paper.Size;
        Color color = PdfColor(AppSettings.ColorBlack);
        _canvas!.SaveState();
        _canvas.SetFillColor(color);
        float spacing = (float)paper.Y.Point / (marks.Count + 1);
        bool oddSheet = _layout.CurrentSheet % 2 == 1;
        bool farSide = marks.Type == PerforationMarkType.Outside ? oddSheet : !oddSheet;
        float x = (float)(farSide ? (double)paper.X - (double)marks.Margin : marks.Margin.Point);
        for (int i = 0; i < marks.Count; i++)
        {
            float y = (float)paper.Y.Point - (spacing * (i + 1));
            _canvas.Circle(x, y, marks.Size.Point);
            _canvas.FillStroke();
        }

        _canvas.RestoreState();
    }

    /// <summary>
    /// The document's ID in small print at the foot of the sheet; for PDF/X also the colour
    /// space and profile. Original: <c>PintaIDDocumento</c>, without the printing company's logo.
    /// </summary>
    partial void DrawDocumentId()
    {
        var paragraph = new Paragraph();
        if (_font != null)
        {
            paragraph.SetFont(_font);
        }

        paragraph.SetFontSize(_settings.Font.Size);
        float x = (float)(_layout.SafetyMargin.X.Point + Unit.FromMillimeter(5.0).Point);
        float y = (float)Unit.FromPoint(_settings.Font.Height).Point;
        string id = _layout.DocumentId;
        if (_group.PDFX)
        {
            id = id + " PDF/X3 " + _other.ColorSpace + " " + _other.ColorProfile?.Description;
        }

        y -= 3f;
        x += 20f;
        paragraph.Add(id);
        paragraph.SetFixedPosition(x, y, (float)_print.Paper.Size.Y.Point);
        _canvas!.SaveState();
        _canvas.SetFillColor(PdfColor(AppSettings.ColorBlack));
        _document!.Add(paragraph);
        _canvas.RestoreState();
    }

    /// <summary>The watermark image stretched over the whole sheet, on top of the cards. Original: <c>PintaMarcaAguaPaginaCompleta</c>.</summary>
    partial void DrawPageWatermark()
    {
        if (_settings.Watermark.Type != WatermarkType.OnPage || !File.Exists(_settings.Watermark.Path))
        {
            return;
        }

        Image watermark = new Image(ImageDataFactory.Create(_settings.Watermark.Path))
            .ScaleAbsolute((float)_print.Paper.Size.X.Point, (float)_print.Paper.Size.Y.Point)
            .SetFixedPosition(1, 0f, 0f);
        _document!.Add(watermark);
    }
}
