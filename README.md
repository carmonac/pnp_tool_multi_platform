# PNP Tool for macOS and Linux

A cross-platform port of **PNP Tool**, the print-and-play utility for laying out
cards, tokens and booklets on printable PDF sheets.

The original PNP Tool is a Windows-only application written by **Jupklass**.
This project brings it to macOS and Linux. It aims
to produce the same PDF output and to read and write the same project files
(`.pnp`, `.pnps`, `.pnpa`, `.pnpc`, `.pnpm`, `.pnpt`) as the Windows version.

> **Status:** early development. Nothing is usable yet.

## Technology

- .NET 11, C#
- [Avalonia](https://github.com/AvaloniaUI/Avalonia) for the user interface
- [iText](https://itextpdf.com/) for PDF generation
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for image processing and
  colour management

## Building

Requires the .NET 11 SDK (the exact version is pinned in `global.json`).

```bash
dotnet build
dotnet test
dotnet run --project src/PnpTool.App
```

## Differences from the Windows version

- No telemetry and no connection to the original update servers.
- The bundled Windows-only helper programs (nanDECK, PosteRazor, AutoCaptura)
  are not included; native replacements are planned where practical.

## Licence

This project is licensed under the GNU Affero General Public License v3.0, see
[LICENSE](LICENSE). This licence is required by the iText library it depends on.

PNP Tool is the work of its original author. This port is not affiliated with
any printing company or professional printing service.
