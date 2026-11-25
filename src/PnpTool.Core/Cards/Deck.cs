using System.Xml.Serialization;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Cards;

/// <summary>The cards of a project. Original: <c>Mazo</c>.</summary>
[XmlRoot("Mazo")]
[XmlType("Mazo")]
public class Deck
{
    /// <summary>Original: the public field <c>Cartas</c>.</summary>
    [XmlArray("Cartas")]
    [XmlArrayItem("Carta")]
    public Card[] Cards = [];

    /// <summary>Total number of cards counting copies. Original: <c>NumeroDeCartas</c>.</summary>
    [XmlElement("NumeroDeCartas")]
    public int TotalCopies { get; set; }

    /// <summary>Receives progress (0–100) while a folder is loaded.</summary>
    [XmlIgnore]
    public Action<double, string>? ReportProgress { get; set; }

    public Deck()
    {
    }

    /// <summary>Creates a deck with room for <paramref name="cardCount"/> cards; the slots start empty.</summary>
    public Deck(int cardCount)
    {
        Cards = new Card[cardCount];
        TotalCopies = cardCount;
    }

    /// <summary>Builds a deck from the merged images of a custom deck.</summary>
    public Deck(CustomCard[] customCards)
        : this()
    {
        foreach (CustomCard custom in customCards)
        {
            Add(new Card
            {
                Copies = custom.Copies,
                Front = new CardFace(custom.MergedImagePath, FaceType.Front, ImageKind.Normal),
            });
        }
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(Deck source)
    {
        Array.Resize(ref Cards, source.Cards.Length);
        TotalCopies = source.TotalCopies;
        for (int i = 0; i < source.Cards.Length; i++)
        {
            Cards[i] = new Card();
            Cards[i].CopyFrom(source.Cards[i]);
        }
    }

    /// <summary>Puts a copy of the card in an existing slot. Original: <c>InsertarCartaEnMazo</c>.</summary>
    public void Insert(Card card, int index)
    {
        Cards[index] = new Card();
        Cards[index].CopyFrom(card);
        TotalCopies += card.Copies;
    }

    /// <summary>Appends a copy of the card. Original: <c>AnadirCartaAMazo(Carta)</c>.</summary>
    public void Add(Card card)
    {
        int index = Cards.Length;
        Array.Resize(ref Cards, index + 1);
        Insert(card, index);
    }

    public void Add(string frontPath, ImageKind frontKind) => Add(frontPath, frontKind, string.Empty, ImageKind.Empty);

    /// <summary>Appends a card from paths; copies come from the front file name.</summary>
    public void Add(string frontPath, ImageKind frontKind, string backPath, ImageKind backKind)
    {
        int originalLength = Cards.Length;
        Array.Resize(ref Cards, originalLength + 1);
        var card = new Card
        {
            Front = new CardFace(frontPath, FaceType.Front, frontKind),
            Back = new CardFace(backPath, FaceType.Back, backKind),
        };
        card.Copies = card.Front.ImageKind == ImageKind.Normal ? Card.CopiesFromFileName(frontPath) : 1;
        Cards[originalLength] = card;
        TotalCopies += card.Copies;
    }

    public void Add(CardFace front, CardFace back, int copies)
    {
        Add(front, copies);
        Cards[^1].Back.CopyFrom(back);
    }

    public void Add(CardFace front, int copies)
    {
        int originalLength = Cards.Length;
        Array.Resize(ref Cards, originalLength + 1);
        var card = new Card();
        card.Front.CopyFrom(front);
        card.Copies = copies;
        Cards[originalLength] = card;
        TotalCopies += copies;
    }

    /// <summary>Original: <c>QuitarTodasLasCartas</c>.</summary>
    public void Clear()
    {
        Array.Resize(ref Cards, 0);
        TotalCopies = 0;
    }

    /// <summary>
    /// Removes a card, deleting its temporary files and lowering the selection counters
    /// kept by the caller. Original: <c>QuitarCartaDeMazo</c>.
    /// </summary>
    public bool RemoveAt(int indexToRemove, ref int selectedFaceCount, ref int selectedPanelCount)
    {
        int originalLength = Cards.Length;
        int target = 0;
        bool removed = false;
        for (int i = 0; i < originalLength; i++)
        {
            if (i != indexToRemove)
            {
                Cards[target] = Cards[i];
                target++;
                continue;
            }

            Card card = Cards[indexToRemove];
            TotalCopies -= card.Copies;
            if (card.Front.Selected)
            {
                selectedFaceCount--;
            }

            if (card.Back.Selected)
            {
                selectedFaceCount--;
            }

            if (card.Selected)
            {
                selectedPanelCount--;
            }

            card.DeleteTempFiles();
            removed = true;
        }

        if ((originalLength > 0) & removed)
        {
            Array.Resize(ref Cards, originalLength - 1);
        }

        return removed;
    }

    /// <summary>
    /// Adds a card per file. With double panels, files are taken in pairs.
    /// Original: <c>GenerarDesdeCarpeta</c>.
    /// </summary>
    /// <remarks>
    /// Kept from the original: in double-panel mode the back is read from the same file as
    /// the front (the second file of the pair is skipped, not used).
    /// </remarks>
    public void LoadFromFiles(string[] sortedFiles, bool doublePanel, CardFace blankFace, bool clearFirst)
    {
        if (clearFirst)
        {
            Clear();
        }

        for (int i = 0; i < sortedFiles.Length; i++)
        {
            string current = sortedFiles[i];
            int copies = Card.CopiesFromFileName(System.IO.Path.GetFileName(current) ?? string.Empty);
            CardFace front = ImageInspector.Current.GetImageProperties(current);
            if (doublePanel)
            {
                var back = new CardFace();
                if (i + 1 < sortedFiles.Length)
                {
                    i++;
                    back = ImageInspector.Current.GetImageProperties(current);
                }
                else
                {
                    back.CopyFrom(blankFace);
                }

                Add(front, back, copies);
            }
            else
            {
                Add(front, copies);
            }

            ReportProgress?.Invoke((double)i / sortedFiles.Length * 100.0, string.Empty);
        }
    }

    /// <summary>Moves a card to another position, shifting the ones in between. Original: <c>DesplazarCarta</c>.</summary>
    public void Move(int sourceIndex, int newIndex)
    {
        if (newIndex == -1)
        {
            newIndex = 0;
        }

        var moved = new Card();
        moved.CopyFrom(Cards[sourceIndex]);
        if (newIndex < sourceIndex)
        {
            for (int i = sourceIndex; i > newIndex; i--)
            {
                var shifted = new Card();
                shifted.CopyFrom(Cards[i - 1]);
                Cards[i] = shifted;
            }
        }
        else
        {
            for (int i = sourceIndex; i < newIndex; i++)
            {
                var shifted = new Card();
                shifted.CopyFrom(Cards[i + 1]);
                Cards[i] = shifted;
            }
        }

        Cards[newIndex] = moved;
    }

    // ---- switching between single panels (one image each) and double panels (front + back)

    /// <summary>
    /// Fills this (empty) deck from a single-panel deck, as double panels.
    /// Original: <c>ConvertirAPanelDoble</c>. <paramref name="blankColor"/> is the colour text ("A_R_G_B") for blank faces.
    /// </summary>
    public void ConvertToDoublePanel(FaceChangeChoice choice, Deck source, string blankColor, bool hasSharedBack, CardFace sharedBack, CardFace blankFace, bool fromUndo)
    {
        switch (choice)
        {
            case FaceChangeChoice.Fronts:
                SinglePanelsAsFronts(blankColor, source, fromUndo);
                break;
            case FaceChangeChoice.Backs:
                SinglePanelsAsBacks(blankColor, source, fromUndo);
                break;
            case FaceChangeChoice.Group:
                GroupSinglePanels(hasSharedBack, sharedBack, blankFace, source, fromUndo);
                break;
            case FaceChangeChoice.Match:
                SinglePanelsWithMatchingBack(source);
                break;
        }
    }

    /// <summary>
    /// Fills this (empty) deck from a double-panel deck, as single panels.
    /// Original: <c>ConvertirAPanelSimple</c>.
    /// </summary>
    public void ConvertToSinglePanel(FaceChangeChoice choice, Deck source, CardFace blankFace, bool hasSharedBack, bool fromUndo)
    {
        switch (choice)
        {
            case FaceChangeChoice.Fronts:
                foreach (Card card in source.Cards)
                {
                    Add(card.Front, blankFace, card.Copies);
                }

                break;
            case FaceChangeChoice.Backs:
                foreach (Card card in source.Cards)
                {
                    Add(card.Back, blankFace, card.Copies);
                }

                break;
            case FaceChangeChoice.Group:
                UngroupDoublePanels(blankFace, source, hasSharedBack);
                break;
        }
    }

    // The original has separate undo and non-undo branches here that do the same thing.
    private void UngroupDoublePanels(CardFace blankFace, Deck source, bool hasSharedBack)
    {
        foreach (Card card in source.Cards)
        {
            Add(card.Front, blankFace, card.Copies);
            if (!hasSharedBack)
            {
                Add(card.Back, blankFace, card.Copies);
            }

            Cards[^1].Front.FaceType = FaceType.Front;
            Cards[^1].Back.FaceType = FaceType.Back;
        }
    }

    private void GroupSinglePanels(bool hasSharedBack, CardFace sharedBack, CardFace blankFace, Deck source, bool fromUndo)
    {
        if (hasSharedBack)
        {
            for (int i = 0; i < source.Cards.Length; i++)
            {
                Add(source.Cards[i]);
                Cards[i].Back.CopyFrom(sharedBack);
            }
        }
        else if (!fromUndo)
        {
            for (int i = 0; i < source.Cards.Length; i += 2)
            {
                if (i + 1 < source.Cards.Length)
                {
                    Add(source.Cards[i].Front, source.Cards[i + 1].Front, source.Cards[i].Copies);
                }
                else
                {
                    Add(source.Cards[i].Front, blankFace, source.Cards[i].Copies);
                }

                Cards[^1].Back.FaceType = FaceType.Back;
            }
        }
        else
        {
            foreach (Card card in source.Cards)
            {
                Add(card.Front, blankFace, card.Copies);
                Add(card.Back, blankFace, card.Copies);
            }
        }
    }

    private void SinglePanelsWithMatchingBack(Deck source)
    {
        foreach (Card original in source.Cards)
        {
            var card = new Card();
            card.CopyFrom(original);
            card.Back.Path = card.Front.Path;
            card.Back.OriginalPath = card.Front.OriginalPath;
            card.Back.ImageKind = card.Front.ImageKind;
            card.Back.Rotation.Angle = card.Front.Rotation.Angle;
            card.Back.Rotation.FlipHorizontal = card.Front.Rotation.FlipHorizontal;
            card.Back.Rotation.FlipVertical = card.Front.Rotation.FlipVertical;
            card.Back.Size.X = card.Front.Size.X;
            card.Back.Size.Y = card.Front.Size.Y;
            Add(card);
        }
    }

    private void SinglePanelsAsBacks(string blankColor, Deck source, bool fromUndo)
    {
        foreach (Card original in source.Cards)
        {
            var card = new Card();
            card.CopyFrom(original);
            if (!fromUndo)
            {
                if (original.Front.ImageKind != ImageKind.Empty)
                {
                    card.Back.CopyFrom(original.Front);
                    card.Back.FaceType = FaceType.Back;
                    card.Front.Path = blankColor;
                    card.Front.ImageKind = ImageKind.Blank;
                }
                else
                {
                    card.Back.Path = string.Empty;
                    card.Back.ImageKind = ImageKind.Empty;
                }

                ResetFace(card.Front, FaceType.Front);
            }

            Add(card);
        }
    }

    private void SinglePanelsAsFronts(string blankColor, Deck source, bool fromUndo)
    {
        foreach (Card original in source.Cards)
        {
            var card = new Card();
            card.CopyFrom(original);
            if (!fromUndo)
            {
                if (original.Front.ImageKind != ImageKind.Empty)
                {
                    card.Back.Path = blankColor;
                    card.Back.ImageKind = ImageKind.Blank;
                }
                else
                {
                    card.Back.Path = string.Empty;
                    card.Back.ImageKind = ImageKind.Empty;
                }

                ResetFace(card.Back, FaceType.Back);
            }

            Add(card);
        }
    }

    private static void ResetFace(CardFace face, FaceType type)
    {
        face.FaceType = type;
        face.Rotation.Angle = 0;
        face.Rotation.FlipHorizontal = false;
        face.Rotation.FlipVertical = false;
        face.Size.X = Unit.Zero;
        face.Size.Y = Unit.Zero;
    }

    // ---- relative paths

    /// <summary>
    /// After loading: turns the stored relative paths of image faces into full paths under
    /// <paramref name="projectFolder"/>. Original: <c>RegeneraRutasRelativas</c>.
    /// </summary>
    public void ResolveRelativePaths(string projectFolder)
    {
        Log.Enter(nameof(ResolveRelativePaths));
        for (int i = 0; i < Cards.Length; i++)
        {
            Log.Write("<Carta>");
            Log.Write("<Indice>" + i + "</Indice>");
            ResolveFacePath(Cards[i].Front, projectFolder);
            ResolveFacePath(Cards[i].Back, projectFolder);
            Log.Write("</Carta>");
        }

        Log.Exit(nameof(ResolveRelativePaths));
    }

    private static void ResolveFacePath(CardFace face, string projectFolder)
    {
        string name = face.FaceType.ToString();
        Log.Write("<" + name + " > ");
        if (face.ImageKind != ImageKind.Normal)
        {
            Log.Write("No Normal</" + name + " > ");
            return;
        }

        face.Path = PathHelper.ResolveRelative(projectFolder, face.Path);
        face.OriginalPath = face.OriginalPath != string.Empty
            ? PathHelper.ResolveRelative(projectFolder, face.OriginalPath)
            : face.Path;
        Log.Write("<RutaOriginal>" + face.OriginalPath + "</RutaOriginal>");
        Log.Write("<RutaRelativa>" + face.Path + "</RutaRelativa></" + name + ">");
    }

    /// <summary>
    /// Before saving: replaces the paths of image faces by paths relative to the project file,
    /// in the form stored in files, and remembers the full paths in the two tables
    /// ([card, 0 = front / 1 = back]). Original: <c>ConvierteRelativasTodasLasRutas</c>.
    /// </summary>
    public void MakePathsRelative(string projectFile, string[,] originalPaths, string[,] paths)
    {
        Log.Enter(nameof(MakePathsRelative));
        for (int i = 0; i < Cards.Length; i++)
        {
            MakeFacePathRelative(Cards[i].Front, i, 0, projectFile, originalPaths, paths);
            MakeFacePathRelative(Cards[i].Back, i, 1, projectFile, originalPaths, paths);
        }

        Log.Exit(nameof(MakePathsRelative));
    }

    private static void MakeFacePathRelative(CardFace face, int index, int side, string projectFile, string[,] originalPaths, string[,] paths)
    {
        if (face.ImageKind != ImageKind.Normal)
        {
            return;
        }

        originalPaths[index, side] = face.OriginalPath;
        face.OriginalPath = PathHelper.ToFile(PathHelper.MakeRelative(projectFile, face.OriginalPath));
        paths[index, side] = face.Path;
        face.Path = PathHelper.ToFile(PathHelper.MakeRelative(projectFile, face.Path));
    }

    /// <summary>
    /// After saving: puts back the full paths remembered by <see cref="MakePathsRelative"/>.
    /// Original: <c>DeshacerRelativasTodasLasRutas</c>.
    /// </summary>
    /// <remarks>
    /// The original only restored a card when its front was an image, and then overwrote the
    /// back with whatever the table held (null for a non-image back). Here each face is
    /// restored on its own, which gives the same result whenever the original was correct.
    /// </remarks>
    public void RestoreFullPaths(string[,] originalPaths, string[,] paths)
    {
        for (int i = 0; i < Cards.Length; i++)
        {
            RestoreFacePath(Cards[i].Front, i, 0, originalPaths, paths);
            RestoreFacePath(Cards[i].Back, i, 1, originalPaths, paths);
        }
    }

    private static void RestoreFacePath(CardFace face, int index, int side, string[,] originalPaths, string[,] paths)
    {
        if (face.ImageKind == ImageKind.Normal)
        {
            face.OriginalPath = originalPaths[index, side];
            face.Path = paths[index, side];
        }
    }

    // ---- selection and checks

    /// <summary>Indexes of the selected cards. Original: <c>LocalizaCartasSeleccionadas</c>.</summary>
    public int[] FindSelectedCards()
    {
        var selected = new List<int>();
        for (int i = 0; i < Cards.Length; i++)
        {
            if (Cards[i].Selected)
            {
                selected.Add(i);
            }
        }

        return [.. selected];
    }

    /// <summary>
    /// Finds the first two selected faces and swaps their contents, each keeping its own side.
    /// The arrays receive the faces and card indexes found. Original: <c>LocalizaCarasMarcadas</c>.
    /// </summary>
    public void SwapSelectedFaces(CardFace[] foundFaces, int[] foundIndexes, bool doublePanel)
    {
        int cardIndex = 0;
        int found = 0;
        do
        {
            if (Cards[cardIndex].Front.Selected)
            {
                foundFaces[found] = Cards[cardIndex].Front;
                foundIndexes[found] = cardIndex;
                found++;
            }

            if (doublePanel && Cards[cardIndex].Back.Selected)
            {
                foundFaces[found] = Cards[cardIndex].Back;
                foundIndexes[found] = cardIndex;
                found++;
            }

            cardIndex++;
        }
        while (cardIndex < Cards.Length && found != 2);

        CardFace first = foundFaces[0];
        ReplaceFace(foundIndexes[0], foundFaces[0].FaceType, foundFaces[1]);
        ReplaceFace(foundIndexes[1], foundFaces[1].FaceType, first);
    }

    private void ReplaceFace(int cardIndex, FaceType side, CardFace content)
    {
        var face = new CardFace();
        face.CopyFrom(content);
        if (side == FaceType.Front)
        {
            face.FaceType = FaceType.Front;
            Cards[cardIndex].Front = face;
        }
        else
        {
            face.FaceType = FaceType.Back;
            Cards[cardIndex].Back = face;
        }
    }

    /// <summary>Recounts <see cref="TotalCopies"/> from the cards. Original: <c>VerificaNumeroImagenes</c>.</summary>
    public void RecountCopies()
    {
        int total = 0;
        foreach (Card card in Cards)
        {
            total += card.Copies;
        }

        TotalCopies = total;
    }

    /// <summary>
    /// Reports a card whose image colour space differs from the project's.
    /// Original: <c>IncongruenciaEspacioColorCartaProyecto</c>.
    /// </summary>
    public AppError CheckColorSpace(int index, ProjectColorSpace projectColorSpace)
    {
        var result = new AppError();
        if (!AppSettings.ShowCmykWarning && !AppSettings.ShowCmykWarningOnce)
        {
            return result;
        }

        AppSettings.ShowCmykWarningOnce = false;
        CardFace front = Cards[index].Front;
        CardFace back = Cards[index].Back;
        bool frontIsImage = front.ImageKind == ImageKind.Normal;
        bool backIsImage = back.ImageKind == ImageKind.Normal;
        switch (projectColorSpace)
        {
            case ProjectColorSpace.RGB:
                if ((frontIsImage && front.IsCmyk) || (backIsImage && back.IsCmyk))
                {
                    result.Kind = ErrorKind.Error;
                    result.AppendResource("CMYK_Not_supported");
                }

                break;
            case ProjectColorSpace.CMYK:
                if ((frontIsImage && !front.IsCmyk) || (backIsImage && !back.IsCmyk))
                {
                    result.Kind = ErrorKind.Error;
                    result.AppendResource("RGB_Not_supported");
                }

                break;
        }

        return result;
    }

    /// <summary>
    /// Sets one face on every card. For fronts, the existing front moves to the back first.
    /// Original: <c>InsertarCarasMasivo</c>.
    /// </summary>
    public void SetFacesOnAllCards(CardFace[] faces, FaceType side)
    {
        for (int i = 0; i < Cards.Length; i++)
        {
            if (side == FaceType.Front)
            {
                Cards[i].Back.CopyFrom(Cards[i].Front);
                Cards[i].Back.FaceType = FaceType.Back;
                Cards[i].Front.CopyFrom(faces[i]);
            }
            else
            {
                Cards[i].Back.CopyFrom(faces[i]);
                Cards[i].Back.FaceType = FaceType.Back;
            }
        }
    }

    /// <summary>Adds rows × columns placeholder cards. Original: <c>GeneraMazoVacio</c>.</summary>
    public void AddBlankGrid(int rows, int columns)
    {
        string blankImage = AppSettings.ImagesFolder + "CartaBlanca.jpg";
        for (int i = 0; i < rows * columns; i++)
        {
            Add(blankImage, ImageKind.Normal, blankImage, ImageKind.Normal);
        }
    }
}
