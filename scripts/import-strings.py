#!/usr/bin/env python3
"""Rebuilds src/PnpTool.Core/Resources/Strings*.resx from the decompiled Windows app.

Run scripts/decompile.sh first. Reads the neutral (English) resources of
ClasesGlobalesPNPTool and its translated satellites from reference/decompiled/ and writes
one .resx per language, keys sorted, so the files compare cleanly between versions.

Usage: scripts/import-strings.py
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "reference", "decompiled")
TARGET = os.path.join(ROOT, "src", "PnpTool.Core", "Resources")

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
"""


def read(path):
    strings = {}
    for data in ET.parse(path).getroot().findall("data"):
        if data.get("type") or data.get("mimetype"):
            sys.exit(f"{path}: {data.get('name')} is not a string")
        strings[data.get("name")] = data.findtext("value") or ""
    return strings


def write(path, strings):
    with open(path, "w", encoding="utf-8", newline="\n") as out:
        out.write(HEADER)
        for name in sorted(strings):
            out.write(f'  <data name="{escape(name, {chr(34): "&quot;"})}" xml:space="preserve">\n')
            out.write(f"    <value>{escape(strings[name])}</value>\n")
            out.write("  </data>\n")
        out.write("</root>\n")


def main():
    neutral = os.path.join(SOURCE, "ClasesGlobalesPNPTool", "ClasesGlobalesPNPTool.Resources.Resources.resx")
    if not os.path.exists(neutral):
        sys.exit("Decompiled resources not found; run scripts/decompile.sh first.")

    os.makedirs(TARGET, exist_ok=True)
    for old in glob.glob(os.path.join(TARGET, "Strings*.resx")):
        os.remove(old)

    strings = read(neutral)
    write(os.path.join(TARGET, "Strings.resx"), strings)
    print(f"Strings.resx: {len(strings)} strings")

    for path in sorted(glob.glob(os.path.join(SOURCE, "_satellites", "*", "*.resx"))):
        language = os.path.basename(os.path.dirname(path))
        translated = read(path)
        write(os.path.join(TARGET, f"Strings.{language}.resx"), translated)
        print(f"Strings.{language}.resx: {len(translated)} strings")


if __name__ == "__main__":
    main()
