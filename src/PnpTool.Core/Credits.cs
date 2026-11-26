namespace PnpTool.Core;

/// <summary>People named together under a heading; <see cref="Flag"/> is the flag file of a language.</summary>
public sealed record CreditGroup(string Title, IReadOnlyList<string> Names, string? Flag = null);

/// <summary>
/// Who made PNP Tool, and the terms of use. Original: the pages <c>equipo.html</c>,
/// <c>Traductores.html</c>, <c>Mecenas.html</c> and <c>Terminos.html</c> of <c>config/webs</c>,
/// shown in a web browser control by <c>WindowSetupPnpTool</c>.
/// </summary>
/// <remarks>
/// The names are those of PNP Tool 20.11.1. The pages were in Spanish only; headings and terms
/// are given here in Spanish and in English. The news page (loaded from the server of a
/// printing company) and the changelog of the Windows versions are not part of the port.
/// </remarks>
public static class Credits
{
    /// <summary>The author of PNP Tool.</summary>
    public const string Author = "Jupklass";

    /// <summary>The Windows version this port follows.</summary>
    public const string OriginalVersion = "20.11.1";

    /// <summary>The author of PNP Tool Multiplatform.</summary>
    public const string AuthorMultiPlatform = "Carmonac";

    /// <summary>The team of the Windows program. Original: <c>equipo.html</c>.</summary>
    public static IReadOnlyList<CreditGroup> Team(bool spanish) =>
    [
        new(spanish ? "Programación y desarrollo" : "Programming and development", [Author]),
        new("UI/UX", ["ZVirusIDH"]),
        new("QA", ["Qualith"]),
        new(spanish ? "Café, psiquiatra, animador, porrista" : "Coffee, psychiatrist, entertainer, cheerleader", ["Benito"]),
        new(spanish ? "Vídeos" : "Videos", ["zhorsan"]),
    ];

    /// <summary>The translators, by language. Original: <c>Traductores.html</c>.</summary>
    public static IReadOnlyList<CreditGroup> Translators { get; } =
    [
        new("Русский", ["ChupVulf", "tikitavy"], "ru"),
        new("Français", ["Desgardel", "Griffin Dark", "Mister Digdug"], "fr"),
        new("English", ["Ryan_c_byrd", "Goofy thing", "Pakoito", "Stephen Ellis"], "en"),
        new("Polski", ["Marcin Rys (marc131)"], "pl"),
        new("Deutsch", ["Micha"], "de"),
        new("中文", ["鸾舆"], "zh"),
        new("Português", ["Rangel Sardinha", "Carlos Santhyago"], "pt"),
        new("Italiano", ["Leonardo Russo"], "it"),
        new("Català", ["Didacqs2"], "cat"),
        new("Nederlands", ["Sreukers"], "nl"),
        new("Tiếng Việt", ["PlayPlus"], "vi"),
        new("Türkçe", ["Furkan Halifeoğlu"], "tr"),
        new("فارسی", ["Amir Kholousi"], "fa"),
    ];

    /// <summary>Those who supported the program with donations. Original: <c>Mecenas.html</c>.</summary>
    public static IReadOnlyList<string> Patrons { get; } =
    [
        "OverMalo", "Jesus Hita", "Isdariel", "Edgar Gallego", "Adrian Baigorri", "Federico Gudiri",
        "Airam Santana", "Oscar Leiton", "Francisco Cañadas", "Aday Santana", "Javier Duarte",
        "Albert Asensio", "El Bourrinos", "Pulgar", "Freddy Sarmiento",
    ];

    /// <summary>Closing line of the three pages above.</summary>
    public static string Thanks(bool spanish) => spanish ? "¡Gracias a todos!" : "Thank you all!";

    /// <summary>What this program is and whose work it is.</summary>
    public static IReadOnlyList<string> About(bool spanish) => spanish
        ?
        [
            "PNP Tool es obra de " + Author + ", que lo escribió para Windows.",
            "Esta es una adaptación a macOS y Linux de PNP Tool " + OriginalVersion + " realizada por " + AuthorMultiPlatform+ ". Lee y escribe los mismos archivos de proyecto y busca dar el mismo PDF.",
            "Las donaciones van al autor original.",
        ]
        :
        [
            "PNP Tool is the work of " + Author + ", who wrote it for Windows.",
            "This is a port to macOS and Linux of PNP Tool " + OriginalVersion + " by " + AuthorMultiPlatform + ". It reads and writes the same project files and aims to give the same PDF.",
            "Donations go to the original author.",
        ];

    /// <summary>Where the source code of this port is, as a line that ends with its address.</summary>
    public static string SourceCode(bool spanish) => (spanish ? "Código fuente: " : "Source code: ") + AppLinks.SourceCode;

    /// <summary>
    /// The terms of use. Original: <c>Terminos.html</c>, without the line about the data sent
    /// to a server at startup, which this port does not send, and with the licence added.
    /// </summary>
    public static IReadOnlyList<string> Terms(bool spanish) => spanish
        ?
        [
            "Este programa es gratuito y busca mantenerse mediante donaciones.",
            "Este programa se entrega tal cual.",
            "No tiene soporte.",
            "Úsalo bajo tu responsabilidad.",
            "Esta versión no envía datos al arrancar ni busca actualizaciones. Solo se conecta a internet cuando lo pides: al abrir un enlace o al buscar cartas.",
            "PNP Tool no guarda ningún tipo de relación con ninguna imprenta ni con ningún servicio de impresión profesional.",
            "Esta versión se distribuye bajo la licencia GNU Affero General Public License v3.0.",
        ]
        :
        [
            "This program is free and supported by donations.",
            "This program is provided as is.",
            "There is no support.",
            "Use at your own risk.",
            "This version sends no data at startup and does not look for updates. It only goes online when you ask it to: to open a link or to search for cards.",
            "PNP Tool is not affiliated with any printing company or professional printing service.",
            "This version is distributed under the GNU Affero General Public License v3.0.",
        ];

    /// <summary>The note on the Adobe colour profiles shipped with the program.</summary>
    public static IReadOnlyList<string> AdobeProfiles(bool spanish) => spanish
        ?
        [
            "Este programa usa perfiles de color ICC de Adobe.",
            "Lee su acuerdo de licencia: " + AppLinks.AdobeIccLicence,
            "Toda la información está en la carpeta de perfiles, en icc/Adobe.",
        ]
        :
        [
            "This tool uses Adobe ICC colour profiles.",
            "Please read their licence agreement: " + AppLinks.AdobeIccLicence,
            "All the information is in the profiles folder, under icc/Adobe.",
        ];
}
