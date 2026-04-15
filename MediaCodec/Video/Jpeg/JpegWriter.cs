namespace MediaCodec.Video.Jpeg;

/// <summary>
/// Собирает валидный JPEG-поток байт из Bitmap
/// Записывает стандартные маркеры JPEG: SOI, APP0, DQT×2, SOF0, DHT×4, SOS, EOI
/// </summary>
public sealed class JpegWriter
{
    #region Маркеры JPEG

    private const byte Marker = 0xFF;
    private const byte SOI = 0xD8;
    private const byte APP0 = 0xE0;
    private const byte DQT = 0xDB;
    private const byte SOF0 = 0xC0;
    private const byte DHT = 0xC4;
    private const byte SOS = 0xDA;
    private const byte EOI = 0xD9;

    #endregion

    #region Публичный API

    /// <summary>
    /// Кодирует <see cref="Bitmap"/> в массив байт JPEG.
    /// </summary>
    public static byte[] Encode(Bitmap bitmap, int quality = 75)
    {
        int w = bitmap.Width;
        int h = bitmap.Height;
        int blocksX = (w + 7) / 8;
        int blocksY = (h + 7) / 8;

        var lumTable = Quantizer.GetTable(true, quality);
        var chromTable = Quantizer.GetTable(false, quality);
        var huffman = new HuffmanCoder();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Фиксированная структура заголовка
        WriteSOI(bw);
        WriteAPP0(bw);
        WriteDQT(bw, lumTable, tableId: 0);
        WriteDQT(bw, chromTable, tableId: 1);
        WriteSOF0(bw, w, h);

        var (lumDcBits, lumDcHV) = HuffmanCoder.GetTableDefinition(isDc: true, isLuminance: true);
        var (lumAcBits, lumAcHV) = HuffmanCoder.GetTableDefinition(isDc: false, isLuminance: true);
        var (chrDcBits, chrDcHV) = HuffmanCoder.GetTableDefinition(isDc: true, isLuminance: false);
        var (chrAcBits, chrAcHV) = HuffmanCoder.GetTableDefinition(isDc: false, isLuminance: false);

        WriteDHT(bw, tcTh: 0x00, lumDcBits, lumDcHV);   // DC яркость
        WriteDHT(bw, tcTh: 0x10, lumAcBits, lumAcHV);   // AC яркость
        WriteDHT(bw, tcTh: 0x01, chrDcBits, chrDcHV);   // DC цветность
        WriteDHT(bw, tcTh: 0x11, chrAcBits, chrAcHV);   // AC цветность

        WriteSosHeader(bw);

        // Данные с энтропийным кодированием
        var bits = new BitWriter(bw);
        int prevDcY = 0, prevDcCb = 0, prevDcCr = 0;

        for (int by = 0; by < blocksY; by++)
            for (int bx = 0; bx < blocksX; bx++)
            {
                var rgb = GetRgbBlock(bitmap, bx * 8, by * 8, w, h);
                var (y, cb, cr) = ColorSpace.RgbBlockToYCbCr(rgb);

                // Y — яркость
                DctTransform.Forward(y);
                Quantizer.Quantize(y, lumTable);
                EncodeBlock(ZigZag.Encode(y), isLuminance: true, ref prevDcY, huffman, bits);

                // Cb — цветность
                DctTransform.Forward(cb);
                Quantizer.Quantize(cb, chromTable);
                EncodeBlock(ZigZag.Encode(cb), isLuminance: false, ref prevDcCb, huffman, bits);

                // Cr — цветность
                DctTransform.Forward(cr);
                Quantizer.Quantize(cr, chromTable);
                EncodeBlock(ZigZag.Encode(cr), isLuminance: false, ref prevDcCr, huffman, bits);
            }

        bits.Flush(); // дополняем последний байт единицами
        WriteEOI(bw);

        bw.Flush();
        return ms.ToArray();
    }

    #endregion

    #region Запись сегментов

    private static void WriteSOI(BinaryWriter bw)
    {
        bw.Write(Marker); bw.Write(SOI);
    }

    private static void WriteAPP0(BinaryWriter bw)
    {
        // JFIF-сегмент, 16 байт итого (length = 16 включает 2-байтовое поле длины)
        bw.Write(Marker); bw.Write(APP0);
        WriteSegmentLength(bw, payloadSize: 14);  // 14 + 2 = 16
        bw.Write((byte)'J');
        bw.Write((byte)'F');
        bw.Write((byte)'I');
        bw.Write((byte)'F');
        bw.Write((byte)0);     // нулевой терминатор
        bw.Write((byte)1);     // версия major = 1
        bw.Write((byte)1);     // версия minor = 1
        bw.Write((byte)0);     // единицы плотности: 0 = без единиц (только соотношение сторон)
        WriteBE16(bw, 1);      // X плотность = 1
        WriteBE16(bw, 1);      // Y плотность = 1
        bw.Write((byte)0);     // ширина миниатюры  = 0 (нет миниатюры)
        bw.Write((byte)0);     // высота миниатюры  = 0
    }

    private static void WriteDQT(BinaryWriter bw, int[,] table, int tableId)
    {
        // Таблица квантизации
        bw.Write(Marker); bw.Write(DQT);
        WriteSegmentLength(bw, payloadSize: 65);
        bw.Write((byte)tableId);

        foreach (int idx in ZigZag.Order)
            bw.Write((byte)table[idx / 8, idx % 8]);
    }

    private static void WriteSOF0(BinaryWriter bw, int width, int height)
    {
        // Заголовок кадра базового DCT
        // Полезная нагрузка: 1(точность) + 2(h) + 2(w) + 1(nComp) + 3×3(Y/Cb/Cr) = 15
        bw.Write(Marker); bw.Write(SOF0);
        WriteSegmentLength(bw, payloadSize: 15);
        bw.Write((byte)8);
        WriteBE16(bw, (ushort)height);
        WriteBE16(bw, (ushort)width);
        bw.Write((byte)3);

        // Компонент: id | коэффициенты прореживания | id таблицы квантования
        // 0x11 = 1H × 1V (без прореживания — 4:4:4)
        bw.Write((byte)1); bw.Write((byte)0x11); bw.Write((byte)0); // Y
        bw.Write((byte)2); bw.Write((byte)0x11); bw.Write((byte)1); // Cb
        bw.Write((byte)3); bw.Write((byte)0x11); bw.Write((byte)1); // Cr
    }

    private static void WriteDHT(BinaryWriter bw, byte tcTh, byte[] bits, byte[] huffval)
    {
        // Полезная нагрузка: 1(tcTh) + 16(BITS) + N(HUFFVAL)
        int n = huffval.Length;
        bw.Write(Marker); bw.Write(DHT);
        WriteSegmentLength(bw, payloadSize: 1 + 16 + n);
        bw.Write(tcTh);                       // (класс таблицы << 4) | id таблицы
        foreach (byte b in bits) bw.Write(b);
        foreach (byte v in huffval) bw.Write(v);
    }

    private static void WriteSosHeader(BinaryWriter bw)
    {
        // Полезная нагрузка: 1(nComp) + 3×2(селекторы компонентов) + 3(Ss/Se/Ah-Al) = 10
        bw.Write(Marker); bw.Write(SOS);
        WriteSegmentLength(bw, payloadSize: 10);
        bw.Write((byte)3);           // 3 компонента в скане

        bw.Write((byte)1); bw.Write((byte)0x00); // Y:  DC=0, AC=0
        bw.Write((byte)2); bw.Write((byte)0x11); // Cb: DC=1, AC=1
        bw.Write((byte)3); bw.Write((byte)0x11); // Cr: DC=1, AC=1

        bw.Write((byte)0);    // Ss = 0  (начало спектральной выборки)
        bw.Write((byte)0x3F); // Se = 63 (конец спектральной выборки)
        bw.Write((byte)0);    // Ah = 0, Al = 0 (нет последовательного приближения)
    }

    private static void WriteEOI(BinaryWriter bw)
    {
        bw.Write(Marker); bw.Write(EOI);
    }

    #endregion

    #region Кодирование блока

    /// <summary>
    /// Кодирует по Хаффману один блок 8×8 (64 коэффициента в порядке зигзага).
    /// Записывает: DC-дифференциал + AC-коэффициенты с кодированием длин серий.
    /// </summary>
    private static void EncodeBlock(
        int[] zz, bool isLuminance, ref int prevDc,
        HuffmanCoder huffman, BitWriter bits)
    {
        // DC-коэффициент (дифференциальное кодирование)
        int dc = zz[0];
        int diff = dc - prevDc;
        prevDc = dc;
        huffman.EncodeDc(diff, isLuminance, bits);

        // AC-коэффициенты (кодирование длин серий)
        // Ищем последний ненулевой AC, чтобы знать, где ставить EOB.
        int lastNonZero = 0;
        for (int i = 63; i >= 1; i--)
            if (zz[i] != 0) { lastNonZero = i; break; }

        if (lastNonZero == 0)
        {
            // Все 63 AC-коэффициента равны нулю → сразу EOB.
            huffman.EncodeAc(0, 0, isLuminance, bits);
            return;
        }

        int run = 0;
        for (int i = 1; i <= lastNonZero; i++)
        {
            if (zz[i] == 0)
            {
                run++;
                if (run == 16)
                {
                    // ZRL: кодируем 16 подряд идущих нулей
                    huffman.EncodeAc(15, 0, isLuminance, bits);
                    run = 0;
                }
            }
            else
            {
                huffman.EncodeAc(run, zz[i], isLuminance, bits);
                run = 0;
            }
        }

        // EOB, если после lastNonZero есть хвостовые нули
        if (lastNonZero < 63)
            huffman.EncodeAc(0, 0, isLuminance, bits);
    }

    #endregion

    #region Извлечение пикселей

    /// <summary>
    /// Извлекает блок 8×8 RGB начиная с позиции (bx, by).
    /// Пиксели за границами изображения берутся от ближайшего края.
    /// Возвращает byte[row, col, channel], channel: 0=R, 1=G, 2=B.
    /// </summary>
    private static byte[,,] GetRgbBlock(Bitmap bmp, int bx, int by, int imgW, int imgH)
    {
        var block = new byte[8, 8, 3];
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
            {
                int px = Math.Min(bx + col, imgW - 1);
                int py = Math.Min(by + row, imgH - 1);
                var c = bmp.GetPixel(px, py);
                block[row, col, 0] = c.R;
                block[row, col, 1] = c.G;
                block[row, col, 2] = c.B;
            }
        return block;
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Записывает 2-байтовую длину сегмента в формате big-endian = payloadSize + 2
    /// (2 байта — само поле длины, согласно спецификации JPEG).
    /// </summary>
    private static void WriteSegmentLength(BinaryWriter bw, int payloadSize)
    {
        int len = payloadSize + 2;
        bw.Write((byte)(len >> 8));
        bw.Write((byte)(len & 0xFF));
    }

    /// <summary>Записывает 16-битное значение в порядке байт big-endian.</summary>
    private static void WriteBE16(BinaryWriter bw, ushort value)
    {
        bw.Write((byte)(value >> 8));
        bw.Write((byte)(value & 0xFF));
    }

    #endregion
}

// BitWriter

/// <summary>
/// Упаковывает биты в байтовый поток, начиная со старшего бита (MSB).
/// Выполняет JPEG byte stuffing: после каждого байта 0xFF записывается 0x00.
/// После записи всех блоков необходимо вызвать Flush() для сброса последнего неполного байта (дополняется единицами).
/// </summary>
public sealed class BitWriter
{
    #region Поля

    private readonly BinaryWriter _bw;
    private uint _buffer;  // накопленные биты
    private int _bits;    // количество накопленных битов

    #endregion

    #region Конструктор

    public BitWriter(BinaryWriter bw) => _bw = bw;

    #endregion

    #region Запись битов

    /// <summary>Записывает <paramref name="count"/> младших битов значения <paramref name="value"/> (MSB первым).</summary>
    public void WriteBits(uint value, int count)
    {
        if (count == 0) return;
        // Маскируем до count бит и сдвигаем в аккумулятор
        uint mask = count < 32 ? (1u << count) - 1u : uint.MaxValue;
        _buffer = (_buffer << count) | (value & mask);
        _bits += count;

        // Сбрасываем полные байты
        while (_bits >= 8)
        {
            _bits -= 8;
            byte b = (byte)(_buffer >> _bits);
            _bw.Write(b);
            if (b == 0xFF)
                _bw.Write((byte)0x00); // byte stuffing по спецификации JPEG
        }
    }

    /// <summary>
    /// Дополняет оставшиеся биты единицами и записывает финальный байт.
    /// Должен вызываться после кодирования всех блоков скана.
    /// </summary>
    public void Flush()
    {
        if (_bits > 0)
        {
            // Дополняем единичными битами (JPEG spec §F.1.2.3)
            int shift = 8 - _bits;
            byte b = (byte)((_buffer << shift) | ((1 << shift) - 1));
            _bw.Write(b);
            if (b == 0xFF)
                _bw.Write((byte)0x00);
            _buffer = 0;
            _bits = 0;
        }
    }

    #endregion
}

// BitReader

/// <summary>
/// Читает отдельные биты из байтового потока, начиная со старшего бита (MSB).
/// Выполняет JPEG byte unstuffing: байт 0x00 после 0xFF молча отбрасывается.
/// </summary>
public sealed class BitReader
{
    #region Поля

    private readonly BinaryReader _br;
    private uint _buffer;
    private int _bits;

    #endregion

    #region Конструктор

    public BitReader(BinaryReader br) => _br = br;

    #endregion

    #region Чтение битов

    /// <summary>Читает один бит (0 или 1) из потока.</summary>
    public int ReadBit()
    {
        if (_bits == 0)
        {
            byte b = _br.ReadByte();
            if (b == 0xFF)
            {
                // Stuffed-байт: потребляем обязательный 0x00
                byte next = _br.ReadByte();
                if (next != 0x00)
                    throw new InvalidDataException(
                        $"Ожидался stuffed 0x00 после 0xFF, получен 0x{next:X2}");
            }
            _buffer = b;
            _bits = 8;
        }

        _bits--;
        return (int)((_buffer >> _bits) & 1);
    }

    /// <summary>Читает <paramref name="count"/> битов и возвращает их как целое число (MSB первым).</summary>
    public int ReadBits(int count)
    {
        int result = 0;
        for (int i = 0; i < count; i++)
            result = (result << 1) | ReadBit();
        return result;
    }

    #endregion
}