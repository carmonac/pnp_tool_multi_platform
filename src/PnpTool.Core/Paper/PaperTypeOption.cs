using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Paper;

/// <summary>An entry of a paper-size list: the text shown and the size it stands for. Original: <c>ComboboxItemTiposPapel</c>.</summary>
public sealed record PaperTypeOption(string Text, PaperType Value)
{
    private const string ResourceKeyPrefix = "ComboBox_TamanosPapel_";

    public override string ToString() => Text;

    /// <summary>
    /// Sizes offered for sheets; with <paramref name="covers"/> the 500 × 330 mm sheet for
    /// book covers is added. Original: <c>CargarTiposPapel</c>, where that entry was labelled
    /// "Soriano_Portadas"; here it is labelled with its size, like the ticket sheets.
    /// </summary>
    public static PaperTypeOption[] ForSheets(bool covers)
    {
        List<PaperTypeOption> options =
        [
            new("A4", PaperType.A4),
            new("A5", PaperType.A5),
            new("A3", PaperType.A3),
            new("A2", PaperType.A2),
            new("A1", PaperType.A1),
            new("A0", PaperType.A0),
            new(StringResources.ForEnum(ResourceKeyPrefix, PaperType.Letter), PaperType.Letter),
            new(StringResources.ForEnum(ResourceKeyPrefix, PaperType.Legal), PaperType.Legal),
            new(StringResources.ForEnum(ResourceKeyPrefix, PaperType.Custom), PaperType.Custom),
        ];
        if (covers)
        {
            options.Add(new("500x330", PaperType.SorianoCovers));
        }

        return [.. options];
    }

    /// <summary>Sizes offered for ticket sheets. Original: <c>CargarTiposPapelTicket</c>.</summary>
    public static PaperTypeOption[] ForTickets() =>
    [
        new("330x480", PaperType.SorianoTickets330x480),
        new("320x450", PaperType.SorianoTickets320x450),
        new("225x320", PaperType.SorianoTickets225x320),
        new("A4", PaperType.A4),
    ];
}
