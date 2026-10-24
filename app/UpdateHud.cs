using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

namespace Gta6Countdown;

/// <summary>
/// Renders a short-lived "update available" toast directly into the baked
/// background, so the notification keeps the desktop composition untouched and
/// costs nothing once it has faded out.
/// </summary>
internal static class UpdateHud
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _useB;

    public static void Begin(string version, int width, int height)
    {
        _ = RunAsync(version, width, height);
    }

    private static async Task RunAsync(string version, int width, int height)
    {
        try
        {
            var resized = await Gate.WaitAsync(0).ConfigureAwait(false);
            if (!resized) return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2.4)).ConfigureAwait(false);

                var start = DateTime.UtcNow;
                const double holdSeconds = 8;
                const double fadeIn = 0.55;
                const double fadeOut = 0.9;

                while (true)
                {
                    var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                    var alpha = elapsed < fadeIn
                        ? elapsed / fadeIn
                        : elapsed < holdSeconds
                            ? 1
                            : 1 - (elapsed - holdSeconds) / fadeOut;

                    alpha = Math.Clamp(alpha, 0, 1);
                    if (alpha <= 0)
                    {
                        await PushPlainBackgroundAsync().ConfigureAwait(false);
                        break;
                    }

                    await ApplyAsync(version, width, height, alpha).ConfigureAwait(false);

                    var delay = elapsed < fadeIn
                        ? 70
                        : elapsed < holdSeconds
                            ? 400
                            : 80;
                    await Task.Delay(delay).ConfigureAwait(false);
                }

                WidgetLog.Write("Update notice finished.");
            }
            finally
            {
                Gate.Release();
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Update notice failed: {ex}");
        }
    }

    private static async Task ApplyAsync(string version, int width, int height, double alpha)
    {
        await Task.Run(() => Draw(version, width, height, alpha)).ConfigureAwait(false);
    }

    private static Task PushPlainBackgroundAsync()
    {
        var url = $"https://gta6.data/{Path.GetFileName(WidgetPaths.BackgroundJpg)}?v={DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
        var script =
            "window.reloadBackground && window.reloadBackground(" +
            System.Text.Json.JsonSerializer.Serialize(url) + ");";

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow main)
                {
                    _ = main.PushBackgroundUrl(script);
                }
            }
            catch (Exception ex)
            {
                WidgetLog.Write($"Restoring background failed: {ex.Message}");
            }
        }));

        return Task.CompletedTask;
    }

    private static void Draw(string version, int width, int height, double alpha)
    {
        var backdrop = WidgetPaths.BackgroundJpg;
        if (!File.Exists(backdrop)) return;

        using var image = new Bitmap(backdrop);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var scale = image.Width / 1920.0;
            var cardWidth = (int)Math.Round(440 * scale);
            var cardHeight = (int)Math.Round(86 * scale);
            var margin = (int)Math.Round(40 * scale);
            // Bottom right, above the progress bar so it never competes with the clock.
            var x = image.Width - cardWidth - margin;
            var y = image.Height - cardHeight - (int)Math.Round(150 * scale);

            var box = new Rectangle(x, y, cardWidth, cardHeight);
            var radius = (int)Math.Round(16 * scale);

            using var path = RoundedRectangle(box, radius);
            using (var shadow = new SolidBrush(Color.FromArgb((int)(120 * alpha), 0, 0, 0)))
            {
                graphics.FillPath(shadow, path);
            }

            using (var fill = new SolidBrush(Color.FromArgb((int)(226 * alpha), 16, 9, 24)))
            {
                graphics.FillPath(fill, path);
            }

            using (var border = new Pen(Color.FromArgb((int)(110 * alpha), 255, 47, 150), Math.Max(1f, (float)(1.6 * scale))))
            {
                graphics.DrawPath(border, path);
            }

            var pad = (int)Math.Round(16 * scale);
            var iconSize = (int)Math.Round(44 * scale);
            var iconRect = new Rectangle(x + pad, y + (cardHeight - iconSize) / 2, iconSize, iconSize);

            using (var iconBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 255, 47, 150)))
            {
                graphics.FillEllipse(iconBrush, iconRect);
            }

            var fontSize = (float)Math.Round(22 * scale);
            var descSize = (float)Math.Round(12 * scale);
            var titleSize = (float)Math.Round(11 * scale);
            using var titleFont = new Font("Segoe UI", titleSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var mainFont = new Font("Microsoft YaHei UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var descFont = new Font("Microsoft YaHei UI", descSize, FontStyle.Regular, GraphicsUnit.Pixel);

            var textX = iconRect.Right + (int)Math.Round(14 * scale);
            var textWidth = cardWidth - (textX - x) - pad;

            using var pinkBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 255, 47, 150));
            using var whiteBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 255, 247, 251));
            using var mutedBrush = new SolidBrush(Color.FromArgb((int)(235 * alpha), 190, 172, 196));

            graphics.DrawString("GTA VI COUNTDOWN", titleFont, pinkBrush, textX, y + pad - (int)Math.Round(2 * scale));
            graphics.DrawString($"\u53d1\u73b0\u65b0\u7248\u672c v{version}", mainFont, whiteBrush, textX, y + pad + (int)Math.Round(16 * scale));

            var format = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            graphics.DrawString(
                "\u524d\u5f80 Release \u9875\u9762\u4e0b\u8f7d\u65b0\u7248\u672c",
                descFont,
                mutedBrush,
                new RectangleF(textX, y + pad + (int)Math.Round(44 * scale), textWidth, descSize * 1.6f),
                format);

            // Inline arrow marker, drawn to avoid embedding extra icon files.
            var arrow = new[]
            {
                new PointF(iconRect.X + iconSize * 0.5f, iconRect.Y + iconSize * 0.30f),
                new PointF(iconRect.X + iconSize * 0.70f, iconRect.Y + iconSize * 0.52f),
                new PointF(iconRect.X + iconSize * 0.56f, iconRect.Y + iconSize * 0.52f),
                new PointF(iconRect.X + iconSize * 0.56f, iconRect.Y + iconSize * 0.72f),
                new PointF(iconRect.X + iconSize * 0.44f, iconRect.Y + iconSize * 0.72f),
                new PointF(iconRect.X + iconSize * 0.44f, iconRect.Y + iconSize * 0.52f),
                new PointF(iconRect.X + iconSize * 0.30f, iconRect.Y + iconSize * 0.52f)
            };
            using (var arrowBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 255, 255, 255)))
            {
                graphics.FillPolygon(arrowBrush, arrow);
            }
        }

        // Write to a scratch file first so the page never reads a half-written JPEG,
        // then hand it to the alternating publisher.
        var tempPath = Path.Combine(WidgetPaths.DataDirectory, "background-hud.tmp");
        image.Save(tempPath, System.Drawing.Imaging.ImageFormat.Jpeg);

        if (!TryPublish(tempPath) && File.Exists(tempPath))
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }

    public static void FinishHud()
    {
        try
        {
            if (File.Exists(WidgetPaths.BackgroundHudJpg))
            {
                File.Delete(WidgetPaths.BackgroundHudJpg);
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Cleaning notice image failed: {ex.Message}");
        }
    }

    private static bool TryPublish(string source)
    {
        // WebView2 keeps the JPEG mapped while it is on screen and the data folder
        // denies in-place overwrites, so alternate between two files that each get
        // a fresh URL in the page.
        bool[] order = { _useB, !_useB };

        for (var step = 0; step < 40; step++)
        {
            var target = order[step % 2] ? WidgetPaths.BackgroundHudJpg : WidgetPaths.BackgroundJpg;
            try
            {
                if (File.Exists(target))
                {
                    try
                    {
                        File.SetAttributes(target, FileAttributes.Normal);
                        File.Delete(target);
                    }
                    catch (Exception ex)
                    {
                        WidgetLog.Write($"Notice target {Path.GetFileName(target)} busy: {ex.Message}");
                        continue;
                    }
                }

                File.Move(source, target);

                var url = $"https://gta6.data/{Path.GetFileName(target)}?v={DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
                var script =
                    "window.reloadBackground && window.reloadBackground(" +
                    System.Text.Json.JsonSerializer.Serialize(url) + ");";

                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (System.Windows.Application.Current.MainWindow is MainWindow main)
                        {
                            _ = main.PushBackgroundUrl(script);
                        }
                    }
                    catch (Exception ex)
                    {
                        WidgetLog.Write($"Notice push failed: {ex.Message}");
                    }
                }));

                _useB = !_useB;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Thread.Sleep(60);
            }
        }

        return false;
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
