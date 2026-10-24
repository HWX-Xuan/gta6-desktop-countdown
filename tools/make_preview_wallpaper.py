"""Generate the original abstract wallpaper used only for repository previews.

The running app never uses this file as the desktop wallpaper; it always reads
the user's current Windows wallpaper. This generated image is a fallback for
file:// previews and keeps the public screenshots free of third-party artwork.
"""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "app" / "assets" / "wallpaper-preview.jpg"

WIDTH, HEIGHT = 2400, 1350
rng = np.random.default_rng(20261118)

yy, xx = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float32)
base = np.zeros((HEIGHT, WIDTH, 3), dtype=np.float32)
base[..., 0] = 9
base[..., 1] = 5
base[..., 2] = 15


def add_glow(cx, cy, radius, color, strength):
    distance = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / radius
    falloff = np.clip(1.0 - distance, 0.0, 1.0) ** 2.2
    for channel in range(3):
        base[..., channel] += color[channel] * falloff * strength


# Cool violet-blue upper left, hot magenta center, warm amber lower right.
add_glow(WIDTH * 0.12, HEIGHT * 0.12, WIDTH * 0.72, (58, 62, 190), 0.95)
add_glow(WIDTH * 0.46, HEIGHT * 0.50, WIDTH * 0.60, (255, 45, 158), 0.58)
add_glow(WIDTH * 0.88, HEIGHT * 0.78, WIDTH * 0.45, (255, 135, 75), 0.34)
add_glow(WIDTH * 0.74, HEIGHT * 0.22, WIDTH * 0.42, (150, 90, 255), 0.34)

# Wide diagonal light shafts.
beam = Image.new("L", (WIDTH, HEIGHT), 0)
beam_draw = ImageDraw.Draw(beam)
beam_draw.polygon(
    [
        (WIDTH * 0.52, 0),
        (WIDTH * 0.68, 0),
        (WIDTH * 0.28, HEIGHT),
        (WIDTH * 0.10, HEIGHT),
    ],
    fill=64,
)
beam = beam.filter(ImageFilter.GaussianBlur(80))
beam_arr = np.asarray(beam, dtype=np.float32) / 255.0
for channel in range(3):
    base[..., channel] += np.array([210, 165, 245])[channel] * beam_arr * 0.22

# Bokeh circles.
bokeh = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
bokeh_draw = ImageDraw.Draw(bokeh)
for _ in range(22):
    cx = rng.uniform(0, WIDTH)
    cy = rng.uniform(0, HEIGHT)
    radius = rng.uniform(40, 220)
    color = rng.choice(
        [
            (255, 105, 180, 38),
            (132, 125, 255, 34),
            (255, 180, 130, 28),
            (255, 220, 240, 22),
        ]
    )
    bokeh_draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=tuple(color))
bokeh = bokeh.filter(ImageFilter.GaussianBlur(55))
base = np.clip(base + np.asarray(bokeh, dtype=np.float32)[..., :3], 0, 255)

# Subtle vertical composition shade.
top_shade = np.clip(1.0 - yy / (HEIGHT * 1.7), 0, 1)[..., None] * 22
base += top_shade

# Fine film grain.
noise = rng.normal(0, 4.5, (HEIGHT, WIDTH, 1))
base = np.clip(base + noise, 0, 255)

image = Image.fromarray(base.astype(np.uint8), "RGB")
image = image.filter(ImageFilter.GaussianBlur(1.2))
OUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUT, quality=92, optimize=True)
print(f"Wrote {OUT} ({image.width}x{image.height})")
