using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Video;

/// <summary>
/// Декодирует raw MJPEG-контейнер, созданный <see cref="MjpegEncoder"/>
///
/// Ожидаемый формат файла:
///   [4]  Magic = "MJPG"
///   [4]  FrameCount  (big-endian uint32)
///   Для каждого кадра:
///     [4]  Длина JPEG в байтах (big-endian uint32)
///     [N]  Байты JPEG
///
/// Для декодирования JPEG используется встроенный GDI+ Bitmap .NET —
/// наш JpegWriter генерирует стандартный JPEG, поэтому любой декодер его прочитает
/// </summary>
public sealed class MjpegDecoder
{
    #region Константы формата

    private static readonly byte[] Magic = "MJPG"u8.ToArray();

    #endregion

    #region Публичный API

    /// <summary>
    /// Возвращает общее количество кадров в файле без загрузки пиксельных данных
    /// </summary>
    public static int ReadFrameCount(string mjpegPath)
    {
        using var fs = new FileStream(mjpegPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);
        ValidateMagic(br);
        return (int)ReadBE32(br);
    }

    /// <summary>
    /// Лениво декодирует кадры из MJPEG-файла, возвращая по одному <see cref="Bitmap"/> за раз
    /// Вызывающая сторона отвечает за освобождение каждого кадра после использования
    /// </summary>
    /// <param name="mjpegPath">Путь к .mjpeg файлу.</param>
    /// <returns>Последовательность декодированных кадров <see cref="Bitmap"/>.</returns>
    public IEnumerable<Bitmap> DecodeFrames(string mjpegPath)
    {
        using var fs = new FileStream(mjpegPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        ValidateMagic(br);
        int count = (int)ReadBE32(br);

        for (int i = 0; i < count; i++)
        {
            uint frameLen = ReadBE32(br);
            byte[] jpegBytes = br.ReadBytes((int)frameLen);

            if (jpegBytes.Length != (int)frameLen)
                throw new EndOfStreamException(
                    $"Файл MJPEG обрезан: ожидалось {frameLen} байт для кадра {i}, получено {jpegBytes.Length}");

            yield return DecodeJpeg(jpegBytes);
        }
    }

    /// <summary>
    /// Декодирует один кадр по индексу (0-based)
    /// Последовательно сканирует файл до нужного кадра — используйте
    /// <see cref="BuildIndex"/> для произвольного доступа при воспроизведении
    /// </summary>
    public Bitmap DecodeFrame(string mjpegPath, int frameIndex)
    {
        using var fs = new FileStream(mjpegPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        ValidateMagic(br);
        int count = (int)ReadBE32(br);

        if (frameIndex < 0 || frameIndex >= count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex),
                $"Индекс кадра {frameIndex} выходит за пределы [0, {count - 1}].");

        for (int i = 0; i <= frameIndex; i++)
        {
            uint frameLen = ReadBE32(br);
            if (i == frameIndex)
            {
                byte[] jpegBytes = br.ReadBytes((int)frameLen);
                return DecodeJpeg(jpegBytes);
            }
            // Пропускаем этот кадр
            fs.Seek(frameLen, SeekOrigin.Current);
        }

        throw new InvalidOperationException("Недостижимый код.");
    }

    /// <summary>
    /// Строит индекс байтовых смещений для всех кадров, обеспечивая произвольный доступ O(1)
    /// Возвращает массив смещений в файле — по одному на кадр
    /// Передайте результат в <see cref="DecodeFrameAtOffset"/> для быстрой перемотки
    /// </summary>
    public static long[] BuildIndex(string mjpegPath)
    {
        using var fs = new FileStream(mjpegPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        ValidateMagic(br);
        int count = (int)ReadBE32(br);
        var offsets = new long[count];

        for (int i = 0; i < count; i++)
        {
            // Запоминаем позицию начала данных JPEG (после 4-байтового поля длины)
            long lenPos = fs.Position;
            uint frameLen = ReadBE32(br);
            offsets[i] = fs.Position;  // начало байт JPEG
            fs.Seek(frameLen, SeekOrigin.Current);
        }

        return offsets;
    }

    /// <summary>
    /// Декодирует кадр по заранее вычисленному смещению в файле (из <see cref="BuildIndex"/>)
    /// Обеспечивает произвольный доступ O(1) для плеера
    /// </summary>
    public static Bitmap DecodeFrameAtOffset(string mjpegPath, long jpegOffset, int jpegLength)
    {
        using var fs = new FileStream(mjpegPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(jpegOffset, SeekOrigin.Begin);
        using var br = new BinaryReader(fs);
        byte[] jpegBytes = br.ReadBytes(jpegLength);
        return DecodeJpeg(jpegBytes);
    }

    /// <summary>Жадно декодирует все кадры в список</summary>
    public List<Bitmap> Decode(string mjpegPath) =>
        DecodeFrames(mjpegPath).ToList();

    /// <summary>
    /// Строит полный индекс кадров: смещение + длина для каждого
    /// Используется плеером для произвольного доступа O(1) без перечитывания заголовков
    /// </summary>
    public static (long Offset, int Length)[] BuildFrameIndex(string mjpegPath)
    {
        using var fs = new FileStream(mjpegPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        ValidateMagic(br);
        int count = (int)ReadBE32(br);
        var index = new (long Offset, int Length)[count];

        for (int i = 0; i < count; i++)
        {
            uint frameLen = ReadBE32(br);
            index[i] = (fs.Position, (int)frameLen);  // смещение на начало JPEG-байт + длина
            fs.Seek(frameLen, SeekOrigin.Current);
        }

        return index;
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Декодирует байты JPEG в <see cref="Bitmap"/> с помощью встроенного GDI+ декодера .NET
    /// Наш JpegWriter генерирует стандартный JPEG, поэтому кастомный декодер не нужен
    /// </summary>
    private static Bitmap DecodeJpeg(byte[] jpegBytes)
    {
        using var ms = new MemoryStream(jpegBytes, writable: false);
        using var tmp = new Bitmap(ms);
        // GDI+ держит внутреннюю ссылку на поток даже для JPEG
        // Клонируем через конструктор копирования — возвращаем независимый объект, не привязанный к MemoryStream после его закрытия (иначе рендер PictureBox бросает исключение)
        return new Bitmap(tmp);
    }

    /// <summary>
    /// Валидация магической подписи в начале файла. Выбрасывает исключение, если файл не соответствует формату MJPEG
    /// </summary>
    /// <param name="br">BinaryReader для чтения данных</param>
    private static void ValidateMagic(BinaryReader br)
    {
        var magic = br.ReadBytes(4);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException(
                $"Файл не является валидным MJPEG. Ожидался magic 'MJPG', получено '{Encoding.ASCII.GetString(magic)}'.");
    }

    /// <summary>
    /// Читает 4 байта в big-endian порядке и возвращает uint32
    /// </summary>
    /// <param name="br">BinaryReader для чтения данных</param>
    /// <returns>Прочитанное значение uint32 в big-endian порядке </returns>
    private static uint ReadBE32(BinaryReader br)
    {
        int b0 = br.ReadByte();
        int b1 = br.ReadByte();
        int b2 = br.ReadByte();
        int b3 = br.ReadByte();
        return (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
    }

    #endregion
}