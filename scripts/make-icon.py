#!/usr/bin/env python3
"""Draws the program icon at full size and builds the macOS icon file from it.

The Windows program only has a 32 pixel icon (a ruler, a craft knife and a cut line), too
small for the Dock, so the same motif is drawn again here. Needs Pillow, and iconutil of
macOS for the .icns file. The results are kept in the repository; run this only to change
the icon:

    python3 scripts/make-icon.py
"""
import math
import pathlib
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parent.parent
SIZE = 1024
SCALE = 4  # drawn larger and reduced, for smooth edges

RULER = (143, 203, 230, 255)
RULER_EDGE = (96, 165, 199, 255)
TICK = (26, 110, 160, 255)
HANDLE = (222, 120, 20, 255)
HANDLE_DARK = (176, 88, 8, 255)
BLADE = (150, 154, 160, 255)
BLADE_LIGHT = (205, 208, 212, 255)
CUT = (176, 88, 8, 255)


def turned(points, angle, centre):
    """The points turned by an angle in degrees (clockwise on screen) about a centre."""
    sine, cosine = math.sin(math.radians(angle)), math.cos(math.radians(angle))
    return [
        (centre[0] + (x * cosine) - (y * sine), centre[1] + (x * sine) + (y * cosine))
        for x, y in points
    ]


def rounded_box(draw, half_length, half_width, radius, angle, centre, fill):
    """A box with round corners, turned about its middle."""
    points = []
    for corner_x, corner_y, start in (
        (half_length - radius, half_width - radius, 0),
        (-half_length + radius, half_width - radius, 90),
        (-half_length + radius, -half_width + radius, 180),
        (half_length - radius, -half_width + radius, 270),
    ):
        for step in range(0, 91, 6):
            a = math.radians(start + step)
            points.append((corner_x + radius * math.cos(a), corner_y + radius * math.sin(a)))
    draw.polygon(turned(points, angle, centre), fill=fill)


def draw_icon():
    s = SIZE * SCALE
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # The plate: the rounded square macOS icons sit on, with a soft top-to-bottom shade.
    inset, radius = 100 * SCALE, 185 * SCALE
    shade = Image.new("RGBA", (s, s))
    shade_draw = ImageDraw.Draw(shade)
    for y in range(s):
        t = y / s
        tone = int(255 - (22 * t))
        shade_draw.line([(0, y), (s, y)], fill=(tone, tone, min(255, tone + 3), 255))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([inset, inset, s - inset, s - inset], radius=radius, fill=255)
    image.paste(shade, (0, 0), mask)

    draw = ImageDraw.Draw(image)
    u = SCALE

    # The cut line, under everything.
    for start in (232, 392, 552):
        draw.rounded_rectangle([start * u, 770 * u, (start + 110) * u, 800 * u], radius=15 * u, fill=CUT)

    # The ruler, from the lower left to the upper right.
    centre = (500 * u, 470 * u)
    rounded_box(draw, 372 * u, 104 * u, 34 * u, -45, centre, RULER_EDGE)
    rounded_box(draw, 360 * u, 92 * u, 26 * u, -45, centre, RULER)
    for index, along in enumerate(range(-280, 281, 80)):
        length = 96 if index % 2 == 0 else 60
        tick = [(along - 11, -92), (along + 11, -92), (along + 11, -92 + length), (along - 11, -92 + length)]
        draw.polygon(turned([(x * u, y * u) for x, y in tick], -45, centre), fill=TICK)

    # The knife over it, from the upper left to the lower right: blade first, handle on top.
    centre = (512 * u, 500 * u)
    blade = [(60, -62), (330, -62), (392, 0), (300, 62), (60, 62)]
    draw.polygon(turned([(x * u, y * u) for x, y in blade], 45, centre), fill=BLADE)
    edge = [(60, 20), (366, 20), (300, 62), (60, 62)]
    draw.polygon(turned([(x * u, y * u) for x, y in edge], 45, centre), fill=BLADE_LIGHT)
    handle_centre = turned([(-150 * u, 0)], 45, centre)[0]
    rounded_box(draw, 240 * u, 96 * u, 90 * u, 45, handle_centre, HANDLE_DARK)
    rounded_box(draw, 228 * u, 84 * u, 80 * u, 45, handle_centre, HANDLE)

    # Nothing may stick out of the plate.
    clipped = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    clipped.paste(image, (0, 0), mask)
    return clipped.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    icon = draw_icon()
    folder = ROOT / "packaging"
    icon.save(folder / "icon-1024.png")
    icon.resize((256, 256), Image.LANCZOS).save(folder / "icon-256.png")

    if shutil.which("iconutil") is None:
        print("iconutil not found: the .icns file was not made")
        return

    with tempfile.TemporaryDirectory() as temporary:
        iconset = pathlib.Path(temporary) / "PnpTool.iconset"
        iconset.mkdir()
        for points in (16, 32, 128, 256, 512):
            icon.resize((points, points), Image.LANCZOS).save(iconset / f"icon_{points}x{points}.png")
            icon.resize((points * 2, points * 2), Image.LANCZOS).save(iconset / f"icon_{points}x{points}@2x.png")
        subprocess.run(
            ["iconutil", "--convert", "icns", "--output", str(folder / "macos" / "PnpTool.icns"), str(iconset)],
            check=True,
        )


if __name__ == "__main__":
    main()
