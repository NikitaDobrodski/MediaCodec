using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Video.Jpeg;

public static class DctTransform
{
    private const int N = 8; // размер блока
    private static readonly float[,] CosTable = BuildCosTable(); // таблица косинусов

    /// <summary>
    /// Построить таблицу косинусов
    /// </summary>
    /// <returns></returns>
    private static float[,] BuildCosTable()
    {
        var t = new float[N, N];
        for (int u = 0; u < N; u++)
            for (int x = 0; x < N; x++)
                t[u, x] = MathF.Cos((2 * x + 1) * u * MathF.PI / 16f);
        return t;
    }

    private static float C(int u) => u == 0 ? 1f / MathF.Sqrt(2f) : 1f; // коэффициенты

    /// <summary>
    /// Прямая 2D DCT-III
    /// </summary>
    /// <param name="block">DCT-коэффициенты</param>
    public static void Forward(float[,] block)
    {
        var tmp = new float[N, N];

        // Шаг 1: DCT по строкам
        for (int x = 0; x < N; x++)
        {
            for (int u = 0; u < N; u++)
            {
                float sum = 0f;
                for (int y = 0; y < N; y++)
                    sum += block[x, y] * CosTable[u, y];
                tmp[x, u] = 0.5f * C(u) * sum;
            }
        }

        // Шаг 2: DCT по столбцам
        for (int u = 0; u < N; u++)
        {
            for (int v = 0; v < N; v++)
            {
                float sum = 0f;
                for (int x = 0; x < N; x++)
                    sum += tmp[x, u] * CosTable[v, x];
                block[v, u] = 0.5f * C(v) * sum;
            }
        }
    }

    /// <summary>
    /// Обратная 2D DCT-III
    /// </summary>
    /// <param name="block">DCT-коэффициенты</param>
    public static void Inverse(float[,] block)
    {
        var tmp = new float[N, N];

        // Шаг 1: IDCT по строкам
        for (int x = 0; x < N; x++)
        {
            for (int y = 0; y < N; y++)
            {
                float sum = 0f;
                for (int u = 0; u < N; u++)
                    sum += C(u) * block[x, u] * CosTable[u, y];
                tmp[x, y] = 0.5f * sum;
            }
        }

        // Шаг 2: IDCT по столбцам
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float sum = 0f;
                for (int v = 0; v < N; v++)
                    sum += C(v) * tmp[v, y] * CosTable[v, x];
                block[x, y] = 0.5f * sum;
            }
        }
    }
}
