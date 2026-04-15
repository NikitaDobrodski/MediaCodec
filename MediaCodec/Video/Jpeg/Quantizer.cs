using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Video.Jpeg;

public static class Quantizer
{
    // Стандартные таблицы JPEG (Annex K)

    private static readonly int[,] LuminanceBase =
    {
        { 16, 11, 10, 16,  24,  40,  51,  61 },
        { 12, 12, 14, 19,  26,  58,  60,  55 },
        { 14, 13, 16, 24,  40,  57,  69,  56 },
        { 14, 17, 22, 29,  51,  87,  80,  62 },
        { 18, 22, 37, 56,  68, 109, 103,  77 },
        { 24, 35, 55, 64,  81, 104, 113,  92 },
        { 49, 64, 78, 87, 103, 121, 120, 101 },
        { 72, 92, 95, 98, 112, 100, 103,  99 }
    };

    private static readonly int[,] ChrominanceBase =
    {
        { 17, 18, 24, 47, 99, 99, 99, 99 },
        { 18, 21, 26, 66, 99, 99, 99, 99 },
        { 24, 26, 56, 99, 99, 99, 99, 99 },
        { 47, 66, 99, 99, 99, 99, 99, 99 },
        { 99, 99, 99, 99, 99, 99, 99, 99 },
        { 99, 99, 99, 99, 99, 99, 99, 99 },
        { 99, 99, 99, 99, 99, 99, 99, 99 },
        { 99, 99, 99, 99, 99, 99, 99, 99 }
    };

    // Public API 

    /// <summary>
    /// Возвращает таблицу квантования, масштабированную под quality [1..100].
    /// quality=100 → минимальные потери, quality=1 → максимальное сжатие.
    /// </summary>
    public static int[,] GetTable(bool isLuminance, int quality)
    {
        quality = Math.Clamp(quality, 1, 100);

        // Стандартная формула масштабирования качества JPEG
        int scale = quality < 50
            ? 5000 / quality
            : 200 - 2 * quality;

        var base_ = isLuminance ? LuminanceBase : ChrominanceBase;
        var table = new int[8, 8];

        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                int val = (base_[r, c] * scale + 50) / 100;
                table[r, c] = Math.Clamp(val, 1, 255);  // минимум 1 — нельзя делить на 0
            }

        return table;
    }

    /// <summary>
    /// Квантует блок 8×8 DCT коэффициентов на месте.
    /// coefficients[r,c] = round(coefficients[r,c] / table[r,c])
    /// </summary>
    public static void Quantize(float[,] coefficients, int[,] table)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                coefficients[r, c] = MathF.Round(coefficients[r, c] / table[r, c]);
    }

    /// <summary>
    /// Деквантует блок 8×8 — обратная операция.
    /// coefficients[r,c] = coefficients[r,c] * table[r,c]
    /// </summary>
    public static void Dequantize(float[,] coefficients, int[,] table)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                coefficients[r, c] *= table[r, c];
    }
}
