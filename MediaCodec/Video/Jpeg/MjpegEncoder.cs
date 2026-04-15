using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MediaCodec.Video.Jpeg;

namespace MediaCodec.Video;

/// <summary>
/// Кодирует последовательность PNG-кадров в кастомный MJPEG-контейнер
///
/// Формат файла:
///   [4]  Magic = "MJPG"
///   [4]  FrameCount  (big-endian uint32)
///   Для каждого кадра:
///     [4]  Длина JPEG в байтах (big-endian uint32)
///     [N]  Байты JPEG
///
/// Каждый JPEG-кадр кодируется с нуля через <see cref="JpegWriter"/>
/// </summary>
public sealed class MjpegEncoder
{
    #region Константы формата

    private static readonly byte[] Magic = "MJPG"u8.ToArray();

    #endregion

    #region Свойства

    /// <summary>Коэффициент качества JPEG от 1 до 100 (по умолчанию 75)</summary>
    public int Quality { get; set; } = 75;

    /// <summary>
    /// Срабатывает после кодирования каждого кадра: (закодировано, всего)
    /// Безопасно обновлять UI — вызов происходит в потоке вызывающей стороны
    /// </summary>
    public event Action<int, int>? ProgressChanged;

    #endregion

    #region Публичный API

    /// <summary>
    /// Кодирует все файлы *.png в <paramref name="framesFolder"/> (отсортированные по имени) в единый MJPEG-файл по пути <paramref name="outputPath"/>
    /// </summary>
    /// <param name="framesFolder">Папка с PNG-кадрами</param>
    /// <param name="outputPath">Путь к создаваемому .mjpeg файлу</param>
    /// <exception cref="InvalidOperationException">Выбрасывается, если PNG-файлы не найдены</exception>
    public void Encode(string framesFolder, string outputPath)
    {
        var files = Directory
            .GetFiles(framesFolder, "*.png")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            throw new InvalidOperationException($"PNG-файлы не найдены в: {framesFolder}");

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs);

        // Заголовок файла 
        bw.Write(Magic);                        // "MJPG"
        WriteBE32(bw, (uint)files.Length);      // количество кадров

        // Кадры 
        for (int i = 0; i < files.Length; i++)
        {
            byte[] jpegBytes = EncodeFrame(files[i]);
            WriteBE32(bw, (uint)jpegBytes.Length);
            bw.Write(jpegBytes);

            ProgressChanged?.Invoke(i + 1, files.Length);
        }
    }

    #endregion

    #region Вспомогательные методы

    private byte[] EncodeFrame(string pngPath)
    {
        using var bmp = new Bitmap(pngPath);
        return JpegWriter.Encode(bmp, Quality);
    }

    private static void WriteBE32(BinaryWriter bw, uint value)
    {
        bw.Write((byte)(value >> 24));
        bw.Write((byte)(value >> 16));
        bw.Write((byte)(value >> 8));
        bw.Write((byte)(value));
    }

    #endregion
}