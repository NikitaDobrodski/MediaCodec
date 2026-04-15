using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec;

/// <summary>
/// Генерирует тестовые PNG кадры для проверки MJPEG кодека.
/// </summary>
public static class TestFrameGenerator
{
    public static void Generate(string outputFolder, int count = 100, int width = 320, int height = 240)
    {
        Directory.CreateDirectory(outputFolder);

        for (int i = 0; i < count; i++)
        {
            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);

            float t = (float)i / count; // 0.0 → 1.0

            // Фон — плавный градиент меняющий цвет от кадра к кадру
            using var bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                ColorFromHsv(t * 360f, 0.6f, 0.3f),
                ColorFromHsv((t * 360f + 120f) % 360f, 0.8f, 0.5f),
                LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(bgBrush, 0, 0, width, height);

            // Движущийся круг
            int cx = (int)(width * (0.2f + 0.6f * (float)Math.Sin(t * Math.PI * 2)));
            int cy = (int)(height * (0.2f + 0.6f * (float)Math.Cos(t * Math.PI * 2)));
            int r = 30;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.White, cx - r, cy - r, r * 2, r * 2);

            // Полосы — хорошо показывают артефакты DCT
            for (int x = 0; x < width; x += 16)
            {
                int alpha = (int)(80 + 80 * Math.Sin(x * 0.3f + t * Math.PI * 4));
                using var pen = new Pen(Color.FromArgb(alpha, 255, 255, 255), 1);
                g.DrawLine(pen, x, 0, x, height);
            }

            // Номер кадра
            g.DrawString($"Frame {i + 1:D3}", new Font("Arial", 12f, FontStyle.Bold),
                Brushes.White, 8, 8);

            string path = Path.Combine(outputFolder, $"frame_{i + 1:D4}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    // HSV → Color (для красивых градиентов)
    private static Color ColorFromHsv(float h, float s, float v)
    {
        h = ((h % 360f) + 360f) % 360f;
        int hi = (int)(h / 60f) % 6;
        float f = h / 60f - (int)(h / 60f);
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        var (r, g, b) = hi switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };

        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
}
