using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Video.Jpeg;

public static class ZigZag
{
    // Стандартный JPEG зигзаг-порядок.
    // Order[i] = row*8 + col — позиция i-го элемента в блоке 8×8
    public static readonly int[] Order =
    {
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    };

    /// <summary>
    /// Читает блок 8×8 в зигзаг-порядке → массив 64 элемента.
    /// </summary>
    public static int[] Encode(float[,] block)
    {
        var result = new int[64];
        for (int i = 0; i < 64; i++)
            result[i] = (int)block[Order[i] / 8, Order[i] % 8];
        return result;
    }

    /// <summary>
    /// Записывает массив 64 элемента обратно в блок 8×8 по зигзаг-порядку.
    /// </summary>
    public static float[,] Decode(int[] zigzag)
    {
        var block = new float[8, 8];
        for (int i = 0; i < 64; i++)
            block[Order[i] / 8, Order[i] % 8] = zigzag[i];
        return block;
    }
}
