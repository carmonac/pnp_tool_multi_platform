using ImageMagick;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Layout.Element;
using PnpTool.Core;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PointD = PnpTool.Core.Primitives.PointD;

namespace PnpTool.Pdf.Layout;

/// <content>
/// The decks made from the user's own pictures: once the document is written, every card gets
/// the symbol of its suit and its number in two corners, and Spanish cards a frame over the
/// picture. Original: <c>FicheroPdfMaquetadoBarajaPersonalizada</c>.
/// </content>
public partial class PdfLayoutWriter
{
    /// <summary>The style of the list that makes a Spanish deck; any other one makes a poker deck.</summary>
    private const int SpanishDeckStyle = 1;

    /// <summary>The size of the numbers, in points.</summary>
    private const float DeckLabelSize = 14f;

    private ImageData? _suitImage;
    private PdfXObject? _suitSymbol;
    private PdfXObject? _suitMask;
    private iText.Kernel.Font.PdfFont? _deckFont;

    /// <summary>
    /// Draws suits and numbers over the fronts of a written document: 40 cards in four
    /// Spanish suits of ten, or 52 in four French suits of thirteen. The document has a sheet
    /// of backs after every sheet of fronts, so every second page is drawn on.
    /// Original: <c>CreaBarajaPersonalizada</c> and <c>PonMascarasBarajaPersonalizada</c>.
    /// </summary>
    private void DrawCustomDeck(string file)
    {
        Suit suit = _group.SubgroupIndex == SpanishDeckStyle ? Suit.Coins : Suit.Hearts;
        (int suitSize, int deckSize, CardSetType set) = suit == Suit.Hearts
            ? (13, 52, CardSetType.Poker)
            : (10, 40, CardSetType.Spanish);

        Log.Enter();
        Progress(0, "Baraja");
        string work = file + "_";
        bool drawn = false;
        using (var source = new PdfDocument(new PdfReader(file)))
        {
            int pages = source.GetNumberOfPages();
            if (pages > 1)
            {
                using var target = new PdfDocument(new PdfWriter(work));
                source.CopyPagesTo(1, pages, target);

                // The standard font of a PDF, which the original gets by naming none.
                _deckFont = PdfTextLine.DefaultFont();
                DrawSuits(target, pages, suit, suitSize, deckSize, set);
                drawn = true;
            }
        }

        if (drawn)
        {
            File.Copy(work, file, overwrite: true);
            DeleteQuietly(work);
        }

        _suitImage = null;
        _suitSymbol = null;
        _suitMask = null;
        _deckFont = null;
        Log.Exit();
    }

    private void DrawSuits(PdfDocument target, int pages, Suit suit, int suitSize, int deckSize, CardSetType set)
    {
        LoadSuit(suit);

        // The pictures were laid out five millimetres in from every side of the card.
        var card = new PointD(
            _front.Size.X.Point + Unit.FromMillimeter(10.0).Point,
            _front.Size.Y.Point + Unit.FromMillimeter(10.0).Point);
        float symbolWidth = (float)_layout.TargetSize.X.Point;
        float symbolHeight = (float)_layout.TargetSize.Y.Point;
        float marginY = (float)(card.Y - _layout.TargetSize.Y.Point - Unit.FromMillimeter(5.0).Point);
        float three = (float)Unit.FromMillimeter(3.0).Point;

        int page = 1;
        int inSuit = 0;
        int cards = 0;
        do
        {
            var canvas = new PdfCanvas(target.GetPage(page));
            bool sheetDone;
            do
            {
                Progress(page * 100 / pages, string.Empty);
                string label = CustomCardSet.CardLabel(inSuit + 1, set);
                bool wide = inSuit > suitSize - 4;
                _layout.SetPositionForCurrentCell();
                float left = (float)_layout.Position.X.Point;
                float bottom = (float)_layout.Position.Y.Point;

                // Upper corner: the symbol and the number above it.
                float x = left;
                float y = bottom + marginY - three;
                canvas.AddXObjectFittedIntoRectangle(_suitSymbol, new Rectangle(x, y, symbolWidth, symbolHeight));
                y += symbolHeight;
                x += wide ? 2 : 4;
                DrawDeckLabel(canvas, label, x, y, upsideDown: false, suit);

                if (_suitMask != null)
                {
                    x = left;
                    canvas.AddXObjectFittedIntoRectangle(_suitMask, new Rectangle(left - 1f, bottom, (float)card.X + 1f, (float)card.Y + 1f));
                }

                // Lower corner: the same, turned upside down.
                x += (float)(card.X - Unit.FromMillimeter(1.0).Point);
                y = (float)(bottom + Unit.FromMillimeter(14.0).Point);
                AffineTransform turned = AffineTransform.GetTranslateInstance(x, y);
                turned.Concatenate(AffineTransform.GetScaleInstance(symbolWidth, symbolHeight));
                turned.Concatenate(AffineTransform.GetRotateInstance(Math.PI));
                float[] matrix = new float[6];
                turned.GetMatrix(matrix);
                canvas.AddImageWithTransformationMatrix(_suitImage, matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5]);
                x -= wide ? 1 : 3;
                y -= three;
                DrawDeckLabel(canvas, label, x, y, upsideDown: true, suit);

                sheetDone = _layout.Advance().SheetDone;
                inSuit++;
                if (inSuit == suitSize)
                {
                    inSuit = 0;
                    suit = CustomCardSet.NextSuit(suit);
                    LoadSuit(suit);
                    symbolWidth = (float)_layout.TargetSize.X.Point;
                    symbolHeight = (float)_layout.TargetSize.Y.Point;
                }

                cards++;
            }
            while (cards <= deckSize - 1 && !sheetDone);

            canvas.Release();
            page += 2;
        }
        // The original goes on to the last sheet; with the cards a deck must have, that is the same.
        while (page < pages && cards < deckSize);
    }

    /// <summary>Loads the symbol of a suit and, for Spanish suits, its frame. Original: <c>ActualizaBarajaPersonalizada</c>.</summary>
    private void LoadSuit(Suit suit)
    {
        Log.Enter(suit.ToString());
        (string mask, string image) = CustomCardSet.SuitImagePaths(suit);
        _suitImage = LoadDeckImage(image);
        _suitSymbol = new Image(_suitImage).GetXObject();
        _suitMask = mask.Length != 0 ? new Image(LoadDeckImage(mask)).GetXObject() : null;
        _layout.TargetSize = CustomCardSet.SymbolSize(suit);
        Log.Exit();
    }

    /// <summary>Original: <c>FicheroPdfMaquetadoBarajaPersonalizada.CargarImagenNueva</c>.</summary>
    private ImageData LoadDeckImage(string path)
    {
        using MagickImage image = _layout.PrepareImage(path, new RotationProperties(), _group.PDFX);
        image.ResetPage();
        (ImageData data, AppError error) = ToImageData((MagickImage)image.Clone(), _group.PDFX);
        if (error.HasError)
        {
            throw new InvalidOperationException(error.Message);
        }

        return data;
    }

    /// <summary>Original: <c>DibujaTextoCarta</c>.</summary>
    private void DrawDeckLabel(PdfCanvas canvas, string text, float x, float y, bool upsideDown, Suit suit)
    {
        // Always an RGB colour, whatever the colour space of the project, as the original.
        ArgbColor color = ArgbColor.Parse(CustomCardSet.SuitColor(suit));
        PdfTextLine.Draw(
            canvas,
            _deckFont!,
            DeckLabelSize,
            text,
            x,
            y,
            DeckLabelSize * 2f,
            upsideDown: upsideDown,
            color: new iText.Kernel.Colors.DeviceRgb(color.R, color.G, color.B));
    }
}
