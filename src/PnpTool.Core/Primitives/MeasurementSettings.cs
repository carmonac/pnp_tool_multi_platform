namespace PnpTool.Core.Primitives;

/// <summary>
/// The measurement system the user works in. Original: the static
/// <c>ConfiguracionPrograma.SistemaMetrico</c> and <c>TerminoSistemaMetrico</c>;
/// the application settings set these values when they load.
/// </summary>
public static class MeasurementSettings
{
    /// <summary>Unit used to show and read lengths in the user interface.</summary>
    public static UnitType Current { get; set; } = UnitType.Millimeter;

    /// <summary>Text appended to lengths when a unit label is requested, for example "mm".</summary>
    public static string Suffix { get; set; } = "mm";
}
