using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Video.Jpeg;

public static class ColorSpace
{
    /// <summary>
    /// Конвертирует блок 8×8 RGB пикселей в три байта Y, Cb, Cr
    /// </summary>
    /// <param name="r">Красная компонента</param>
    /// <param name="g">Зеленая компонента</param>
    /// <param name="b">Синяя компонента</param>
    /// <returns>Кортеж с компонентами Y, Cb и Cr</returns>
    public static (byte Y, byte Cb, byte Cr) RgbToYCbCr(byte r, byte g, byte b)
    {
        float rf = r, gf = g, bf = b;

        float y = 0.299f * rf + 0.587f * gf + 0.114f * bf;
        float cb = -0.1687f * rf - 0.3313f * gf + 0.5f * bf + 128f;
        float cr = 0.5f * rf - 0.4187f * gf - 0.0813f * bf + 128f;

        return (Clamp(y), Clamp(cb), Clamp(cr));
    }

    /// <summary>
    /// Конвертирует блок 8×8 RGB пикселей в три байта Y, Cb, Cr
    /// </summary>
    /// <param name="rgb">Блок 8×8 RGB пикселей</param>
    /// <returns>Кортеж с компонентами Y, Cb и Cr</returns>
    public static (float[,] Y, float[,] Cb, float[,] Cr)
        RgbBlockToYCbCr(byte[,,] rgb)
    {
        var Y = new float[8, 8];
        var Cb = new float[8, 8];
        var Cr = new float[8, 8];

        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
            {
                var (y, cb, cr) = RgbToYCbCr(rgb[row, col, 0],
                                              rgb[row, col, 1],
                                              rgb[row, col, 2]);
                Y[row, col] = y - 128f;   // level shift для DCT
                Cb[row, col] = cb - 128f;
                Cr[row, col] = cr - 128f;
            }

        return (Y, Cb, Cr);
    }

    /// <summary>
    /// Конвертирует блок 8×8 Y, Cb, Cr в блок RGB пикселей
    /// </summary>
    /// <param name="y">Компонента Y</param>
    /// <param name="cb">Компонента Cb</param>
    /// <param name="cr">Компонента Cr</param>
    /// <returns>Кортеж с компонентами R, G и B</returns>
    public static (byte R, byte G, byte B) YCbCrToRgb(byte y, byte cb, byte cr)
    {
        float yf = y;
        float cbf = cb - 128f;
        float crf = cr - 128f;

        float r = yf + 1.402f * crf;
        float g = yf - 0.34414f * cbf - 0.71414f * crf;
        float bf = yf + 1.772f * cbf;

        return (Clamp(r), Clamp(g), Clamp(bf));
    }

    /// <summary>
    /// Конвертирует блок 8×8 Y, Cb, Cr в блок RGB пикселей
    /// </summary>
    /// <param name="y">Компонента Y</param>
    /// <param name="cb">Компонента Cb</param>
    /// <param name="cr">Компонента Cr</param>
    /// <returns>Блок 8×8 RGB пикселей</returns>
    public static byte[,,] YCbCrBlockToRgb(float[,] y, float[,] cb, float[,] cr)
    {
        var rgb = new byte[8, 8, 3];

        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
            {
                var (r, g, b) = YCbCrToRgb(
                    Clamp(y[row, col] + 128f),
                    Clamp(cb[row, col] + 128f),
                    Clamp(cr[row, col] + 128f));

                rgb[row, col, 0] = r;
                rgb[row, col, 1] = g;
                rgb[row, col, 2] = b;
            }

        return rgb;
    }

    private static byte Clamp(float v) =>
        (byte)Math.Clamp((int)MathF.Round(v), 0, 255);
}