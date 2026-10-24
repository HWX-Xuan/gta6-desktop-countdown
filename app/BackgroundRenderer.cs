using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Gta6Countdown;

/// <summary>
/// Bakes the wallpaper and all static visual effects into one JPEG. Rendering a
/// single image is dramatically cheaper than compositing several full-screen
/// CSS blurs, blend modes and animations every frame.
/// </summary>
internal static class BackgroundRenderer
{
    private const long JpegQuality = 96L;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string _lastKey = string.Empty;

    public static (int Width, int Height) GetRenderSize(double dpiScaleX, double dpiScaleY)
    {
        var width = ScreenMetrics.PrimaryWidth;
        var height = ScreenMetrics.PrimaryHeight;

        // Fall back to the DIP extent times the monitor scale when Windows reports
        // no usable metrics; the result is the same physical pixel size.
        if (width <= 0 || height <= 0)
        {
            width = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpiScaleX);
            height = (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpiScaleY);
        }

        return (Math.Max(1, width), Math.Max(1, height));
    }

    public static async Task<bool> RenderAsync(
        WidgetSettings settings,
        string wallpaperPath,
        int width,
        int height)
    {
        if (!File.Exists(wallpaperPath)) return false;

        var clean = settings.Sanitized();
        var key = BuildKey(clean, wallpaperPath, width, height);
        if (key == _lastKey && File.Exists(WidgetPaths.BackgroundJpg)) return false;

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (key == _lastKey && File.Exists(WidgetPaths.BackgroundJpg)) return false;

            await Task.Run(() => Render(clean, wallpaperPath, width, height)).ConfigureAwait(false);
            _lastKey = key;
            WidgetLog.Write($"Background rendered at {width}x{height} -> {WidgetPaths.BackgroundJpg}");
            return true;
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Background render failed: {ex}");
            return false;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string BuildKey(WidgetSettings settings, string wallpaperPath, int width, int height)
    {
        var info = new FileInfo(wallpaperPath);
        return string.Join(
            "|",
            width,
            height,
            wallpaperPath,
            info.Length,
            info.LastWriteTimeUtc.Ticks,
            settings.Blur.ToString("0.##"),
            settings.Brightness.ToString("0.###"),
            settings.Saturation.ToString("0.###"),
            settings.Glow.ToString("0.###"),
            settings.Grain.ToString("0.###"),
            settings.Vignette.ToString("0.###"),
            settings.Accent);
    }

    private static void Render(WidgetSettings settings, string wallpaperPath, int width, int height)
    {
        Directory.CreateDirectory(WidgetPaths.DataDirectory);

        using var source = new Bitmap(wallpaperPath);
        using var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(target))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.None;

            var scale = Math.Max(width / (double)source.Width, height / (double)source.Height);
            var sourceWidth = width / scale;
            var sourceHeight = height / scale;
            var sourceX = (source.Width - sourceWidth) / 2.0;
            var sourceY = (source.Height - sourceHeight) / 2.0;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, width, height),
                new RectangleF((float)sourceX, (float)sourceY, (float)sourceWidth, (float)sourceHeight),
                GraphicsUnit.Pixel);
        }

        var pixels = ReadPixels(target);
        var temp = new byte[pixels.Length];

        // The CSS blur radius was authored for a 1920-wide canvas; scale it so
        // larger screens keep the same visual softness.
        var sizeScale = width / 1920.0;
        var radius = (int)Math.Round(settings.Blur * 0.32 * sizeScale);
        if (radius > 0)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                BoxBlurHorizontal(pixels, temp, width, height, radius);
                BoxBlurVertical(temp, pixels, width, height, radius);
            }
        }

        ApplyGradeAndEffects(pixels, settings, width, height);
        WritePixels(target, pixels);

        var tempPath = WidgetPaths.BackgroundJpg + ".tmp";
        var codec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(item => item.FormatID == ImageFormat.Jpeg.Guid);
        using (var parameters = new EncoderParameters(1))
        {
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
            if (codec is not null)
            {
                target.Save(tempPath, codec, parameters);
            }
            else
            {
                target.Save(tempPath, ImageFormat.Jpeg);
            }
        }

        File.Move(tempPath, WidgetPaths.BackgroundJpg, true);
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);
        try
        {
            var packed = new byte[bitmap.Width * bitmap.Height * 4];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(
                    IntPtr.Add(data.Scan0, y * data.Stride),
                    packed,
                    y * bitmap.Width * 4,
                    bitmap.Width * 4);
            }

            return packed;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap bitmap, byte[] packed)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(
                    packed,
                    y * bitmap.Width * 4,
                    IntPtr.Add(data.Scan0, y * data.Stride),
                    bitmap.Width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void BoxBlurHorizontal(byte[] source, byte[] destination, int width, int height, int radius)
    {
        var prefix = new int[(width + 1) * 4];
        for (var y = 0; y < height; y++)
        {
            Array.Clear(prefix, 0, prefix.Length);
            for (var x = 0; x < width; x++)
            {
                var sourceIndex = (y * width + x) * 4;
                var prefixIndex = (x + 1) * 4;
                prefix[prefixIndex] = prefix[prefixIndex - 4] + source[sourceIndex];
                prefix[prefixIndex + 1] = prefix[prefixIndex - 3] + source[sourceIndex + 1];
                prefix[prefixIndex + 2] = prefix[prefixIndex - 2] + source[sourceIndex + 2];
                prefix[prefixIndex + 3] = prefix[prefixIndex - 1] + source[sourceIndex + 3];
            }

            for (var x = 0; x < width; x++)
            {
                var left = Math.Max(0, x - radius);
                var right = Math.Min(width - 1, x + radius);
                var count = right - left + 1;
                var leftIndex = left * 4;
                var rightIndex = (right + 1) * 4;
                var targetIndex = (y * width + x) * 4;
                destination[targetIndex] = (byte)((prefix[rightIndex] - prefix[leftIndex]) / count);
                destination[targetIndex + 1] = (byte)((prefix[rightIndex + 1] - prefix[leftIndex + 1]) / count);
                destination[targetIndex + 2] = (byte)((prefix[rightIndex + 2] - prefix[leftIndex + 2]) / count);
                destination[targetIndex + 3] = 255;
            }
        }
    }

    private static void BoxBlurVertical(byte[] source, byte[] destination, int width, int height, int radius)
    {
        var prefix = new int[(height + 1) * 4];
        for (var x = 0; x < width; x++)
        {
            Array.Clear(prefix, 0, prefix.Length);
            for (var y = 0; y < height; y++)
            {
                var sourceIndex = (y * width + x) * 4;
                var prefixIndex = (y + 1) * 4;
                prefix[prefixIndex] = prefix[prefixIndex - 4] + source[sourceIndex];
                prefix[prefixIndex + 1] = prefix[prefixIndex - 3] + source[sourceIndex + 1];
                prefix[prefixIndex + 2] = prefix[prefixIndex - 2] + source[sourceIndex + 2];
                prefix[prefixIndex + 3] = prefix[prefixIndex - 1] + source[sourceIndex + 3];
            }

            for (var y = 0; y < height; y++)
            {
                var top = Math.Max(0, y - radius);
                var bottom = Math.Min(height - 1, y + radius);
                var count = bottom - top + 1;
                var topIndex = top * 4;
                var bottomIndex = (bottom + 1) * 4;
                var targetIndex = (y * width + x) * 4;
                destination[targetIndex] = (byte)((prefix[bottomIndex] - prefix[topIndex]) / count);
                destination[targetIndex + 1] = (byte)((prefix[bottomIndex + 1] - prefix[topIndex + 1]) / count);
                destination[targetIndex + 2] = (byte)((prefix[bottomIndex + 2] - prefix[topIndex + 2]) / count);
                destination[targetIndex + 3] = 255;
            }
        }
    }

    private static void ApplyGradeAndEffects(byte[] pixels, WidgetSettings settings, int width, int height)
    {
        var accent = ParseColor(settings.Accent);
        var glow = settings.Glow;
        var brightness = settings.Brightness;
        var saturation = settings.Saturation;
        var vignette = settings.Vignette;
        var grainAmount = settings.Grain;
        var random = 0xA341316Cu;

        // The desktop crop can be narrower than the clean 16:10 source, which pulls
        // the subject away from the frame center. Keep it centered instead.
        var sourceCenterX = 0.5;

        for (var y = 0; y < height; y++)
        {
            var ny = (y + 0.5) / height;
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                var b = pixels[index] / 255.0;
                var g = pixels[index + 1] / 255.0;
                var r = pixels[index + 2] / 255.0;

                var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                r = luminance + (r - luminance) * saturation;
                g = luminance + (g - luminance) * saturation;
                b = luminance + (b - luminance) * saturation;

                r = ((r - 0.5) * 1.08 + 0.5) * brightness;
                g = ((g - 0.5) * 1.08 + 0.5) * brightness;
                b = ((b - 0.5) * 1.08 + 0.5) * brightness;

                var nx = (x + 0.5) / width;

                // Accent bloom in the middle of the frame.
                var accentDistance = Square((nx - 0.5) / 0.62) + Square((ny - 0.49) / 0.55);
                if (accentDistance < 1)
                {
                    var falloff = Square(1 - accentDistance) * 0.17 * glow;
                    r += accent.R / 255.0 * falloff;
                    g += accent.G / 255.0 * falloff;
                    b += accent.B / 255.0 * falloff;
                }

                // Cool light from the upper-left, warm light from the lower-right.
                var blueDistance = Square((nx - 0.10) / 0.55) + Square((ny - 0.08) / 0.55);
                if (blueDistance < 1)
                {
                    var falloff = Square(1 - blueDistance) * 0.12;
                    r += 0.27 * falloff;
                    g += 0.34 * falloff;
                    b += 1.0 * falloff;
                }

                var amberDistance = Square((nx - 0.92) / 0.50) + Square((ny - 0.91) / 0.50);
                if (amberDistance < 1)
                {
                    var falloff = Square(1 - amberDistance) * 0.09;
                    r += 1.0 * falloff;
                    g += 0.58 * falloff;
                    b += 0.30 * falloff;
                }

                // Dark top and bottom bands keep the typography readable.
                var topShade = Math.Max(0, 1 - ny / 0.42) * 0.66;
                var bottomShade = Math.Max(0, (ny - 0.55) / 0.45) * 0.82;
                var shade = Math.Max(topShade, bottomShade);

                // Vignette, biased by the (possibly narrower) source crop.
                var dx = (nx - sourceCenterX) * 2;
                var dy = (ny - 0.5) * 2;
                var edge = Math.Max(0, Math.Sqrt(dx * dx + dy * dy) - 0.45) / 0.95;
                var vignetteFactor = Math.Min(1, edge) * vignette * 0.55;

                var factor = (1 - shade) * (1 - vignetteFactor);
                r *= factor;
                g *= factor;
                b *= factor;

                // A subtle violet grade ties the wallpaper to the neon accent.
                r = (r + 0.012) * 0.93;
                g = (g + 0.004) * 0.93;
                b = (b + 0.025) * 0.93;

                random ^= random << 13;
                random ^= random >> 17;
                random ^= random << 5;
                var noise = (((random & 0xFF) / 255.0) - 0.5) * grainAmount * 0.55;
                r += noise;
                g += noise;
                b += noise;

                pixels[index] = ClampByte(b);
                pixels[index + 1] = ClampByte(g);
                pixels[index + 2] = ClampByte(r);
                pixels[index + 3] = 255;
            }
        }
    }

    private static double Square(double value) => value * value;

    private static byte ClampByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value * 255.0), 0, 255);

    private static Color ParseColor(string? hex)
    {
        try
        {
            return ColorTranslator.FromHtml(WidgetSettings.NormalizeAccent(hex));
        }
        catch
        {
            return Color.FromArgb(255, 47, 150);
        }
    }
}
