#!/usr/bin/env python3
"""Regenerate the website's raster brand assets from docs/logo.png.

Run manually (needs Pillow). The outputs are committed; the deploy pipeline does
NOT run this, so the site build stays stdlib-only.

    pip install pillow
    python3 scripts/make-brand-assets.py
"""
import shutil
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow is required: pip install pillow")

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "docs" / "logo.png"          # 512x512 RGBA, transparent
OUT = ROOT / "site" / "assets" / "img"
NAVY = (11, 15, 25)                       # #0b0f19, same as theme_color

FONT_BOLD = [
    "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    "C:/Windows/Fonts/arialbd.ttf",
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
]
FONT_REG = [p.replace("Bold", "Regular").replace("-Bold", "") for p in FONT_BOLD] + [
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    "C:/Windows/Fonts/arial.ttf",
]


def font(cands, size):
    for c in cands:
        if Path(c).exists():
            return ImageFont.truetype(c, size)
    return ImageFont.load_default()


def trimmed(img):
    return img.crop(img.getbbox())


def fit(logo, box):
    """Logo scaled to fit a square box, centred, transparent."""
    t = trimmed(logo)
    s = min(box / t.width, box / t.height)
    t = t.resize((max(1, round(t.width * s)), max(1, round(t.height * s))), Image.LANCZOS)
    c = Image.new("RGBA", (box, box), (0, 0, 0, 0))
    c.paste(t, ((box - t.width) // 2, (box - t.height) // 2), t)
    return c


def on_navy(size, logo, pad_ratio):
    bg = Image.new("RGBA", (size, size), NAVY + (255,))
    inner = round(size * (1 - 2 * pad_ratio))
    mark = fit(logo, inner)
    bg.paste(mark, ((size - inner) // 2, (size - inner) // 2), mark)
    return bg.convert("RGB")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    logo = Image.open(SRC).convert("RGBA")

    # Header mark: transparent, tight crop, 64px tall (retina 2x).
    t = trimmed(logo)
    w = max(1, round(t.width * (64 / t.height)))
    t.resize((w, 64), Image.LANCZOS).save(OUT / "logo-64.png", optimize=True)
    # PWA / manifest icons: padded on navy so they survive circular masks.
    on_navy(192, logo, 0.16).save(OUT / "icon-192.png", optimize=True)
    on_navy(512, logo, 0.16).save(OUT / "icon-512.png", optimize=True)
    # iOS ignores transparency, so opaque 180x180.
    on_navy(180, logo, 0.14).save(OUT / "apple-touch-icon.png", optimize=True)

    # Social card, 1200x630: brand + the site's real tagline. No fake UI.
    W, H = 1200, 630
    card = Image.new("RGB", (W, H), NAVY)
    d = ImageDraw.Draw(card)
    # soft blue/cyan glow (cheap radial gradient, no numpy)
    glow = Image.new("RGB", (W, H), NAVY)
    gd = ImageDraw.Draw(glow)
    for r in range(520, 0, -8):
        k = (1 - r / 520) ** 2
        col = tuple(round(a + (b - a) * k * 0.55) for a, b in zip(NAVY, (37, 99, 235)))
        gd.ellipse((880 - r, 150 - r, 880 + r, 150 + r), fill=col)
    card = glow
    d = ImageDraw.Draw(card)
    mark = fit(logo, 300)
    card.paste(mark, (790, 165), mark)
    d.text((80, 150), "FCMS Pro", font=font(FONT_BOLD, 96), fill=(244, 247, 252))
    d.text((84, 275), "Freelance commission management", font=font(FONT_REG, 36), fill=(170, 184, 208))
    d.text((84, 322), "that stays on your computer.", font=font(FONT_REG, 36), fill=(170, 184, 208))
    d.text((84, 450), "Free  \u00b7  Open source  \u00b7  No account  \u00b7  No telemetry",
           font=font(FONT_REG, 28), fill=(110, 168, 254))
    d.text((84, 520), "Windows  \u00b7  macOS (Apple Silicon)  \u00b7  Linux",
           font=font(FONT_REG, 26), fill=(130, 144, 168))
    card.save(OUT / "og-card.png", optimize=True)

    # Favicon: reuse the existing multi-size .ico (16/32/48) unchanged.
    shutil.copyfile(ROOT / "docs" / "favicon.ico", ROOT / "site" / "favicon.ico")
    print("wrote", ", ".join(sorted(p.name for p in OUT.iterdir())), "+ favicon.ico")


if __name__ == "__main__":
    main()
