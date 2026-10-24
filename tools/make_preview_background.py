"""Create the baked background used by repository screenshots.

This mirrors the C# BackgroundRenderer closely enough for README previews. The
running app always generates its own background from the user's wallpaper.
"""

from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "app" / "assets" / "wallpaper-preview.jpg"
OUT = ROOT / "app" / "assets" / "background-preview.jpg"

WIDTH, HEIGHT = 1920, 1200
image = Image.open(SOURCE).convert("RGB")
scale = max(WIDTH / image.width, HEIGHT / image.height)
resized = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
left = (resized.width - WIDTH) // 2
top = (resized.height - HEIGHT) // 2
image = resized.crop((left, top, left + WIDTH, top + HEIGHT))
image = image.filter(ImageFilter.GaussianBlur(11))

pixels = np.asarray(image, dtype=np.float32) / 255.0
yy, xx = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float32)
nx = (xx + 0.5) / WIDTH
ny = (yy + 0.5) / HEIGHT

# Match the default WidgetSettings values.
brightness = 0.52
saturation = 1.4
glow = 1.0
vignette = 0.9
grain = 0.085
accent = np.array([255, 47, 150], dtype=np.float32) / 255.0

luminance = 0.2126 * pixels[..., 0] + 0.7152 * pixels[..., 1] + 0.0722 * pixels[..., 2]
pixels = luminance[..., None] + (pixels - luminance[..., None]) * saturation
pixels = (pixels - 0.5) * 1.08 + 0.5
pixels *= brightness

accent_distance = ((nx - 0.5) / 0.62) ** 2 + ((ny - 0.49) / 0.55) ** 2
accent_falloff = np.clip(1 - accent_distance, 0, 1) ** 2 * 0.17 * glow
pixels += accent[None, None, :] * accent_falloff[..., None]

blue_distance = ((nx - 0.10) / 0.55) ** 2 + ((ny - 0.08) / 0.55) ** 2
blue_falloff = np.clip(1 - blue_distance, 0, 1) ** 2 * 0.12
pixels += np.array([0.27, 0.34, 1.0], dtype=np.float32)[None, None, :] * blue_falloff[..., None]

amber_distance = ((nx - 0.92) / 0.50) ** 2 + ((ny - 0.91) / 0.50) ** 2
amber_falloff = np.clip(1 - amber_distance, 0, 1) ** 2 * 0.09
pixels += np.array([1.0, 0.58, 0.30], dtype=np.float32)[None, None, :] * amber_falloff[..., None]

top_shade = np.clip(1 - ny / 0.42, 0, 1) * 0.66
bottom_shade = np.clip((ny - 0.55) / 0.45, 0, 1) * 0.82
shade = np.maximum(top_shade, bottom_shade)

dx = (nx - 0.5) * 2
dy = (ny - 0.5) * 2
edge = np.clip((np.sqrt(dx * dx + dy * dy) - 0.45) / 0.95, 0, 1)
vignette_factor = edge * vignette * 0.55

pixels *= ((1 - shade) * (1 - vignette_factor))[..., None]
pixels = (pixels + np.array([0.012, 0.004, 0.025], dtype=np.float32)[None, None, :]) * 0.93

rng = np.random.default_rng(20261118)
noise = (rng.random((HEIGHT, WIDTH, 1), dtype=np.float32) - 0.5) * grain * 0.55
pixels = np.clip(pixels + noise, 0, 1)

Image.fromarray((pixels * 255).round().astype(np.uint8), "RGB").save(
    OUT, quality=92, optimize=True
)
print(f"Wrote {OUT} ({WIDTH}x{HEIGHT})")
