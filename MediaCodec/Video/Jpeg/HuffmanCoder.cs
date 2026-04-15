namespace MediaCodec.Video.Jpeg;

/// <summary>
/// Кодер Хаффмана для формата JPEG
/// </summary>
public sealed class HuffmanCoder
{
    #region Standard JPEG Huffman tables (Annex K)

    // BITS[i]   = number of codes of length (i+1), i in [0..15]
    // HUFFVAL[] = symbols in order, grouped by code length

    private static readonly byte[] LumaDcBits = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] LumaDcHuffval = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

    private static readonly byte[] LumaAcBits = { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 125 };
    private static readonly byte[] LumaAcHuffval =
    {
        0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,0x21,0x31,0x41,0x06,0x13,0x51,0x61,
        0x07,0x22,0x71,0x14,0x32,0x81,0x91,0xa1,0x08,0x23,0x42,0xb1,0xc1,0x15,0x52,
        0xd1,0xf0,0x24,0x33,0x62,0x72,0x82,0x09,0x0a,0x16,0x17,0x18,0x19,0x1a,0x25,
        0x26,0x27,0x28,0x29,0x2a,0x34,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,0x45,
        0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,0x64,
        0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,0x83,
        0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,
        0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,0xb5,0xb6,
        0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,0xd2,0xd3,
        0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe1,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,0xe8,
        0xe9,0xea,0xf1,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa
    };

    private static readonly byte[] ChromaDcBits = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
    private static readonly byte[] ChromaDcHuffval = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

    private static readonly byte[] ChromaAcBits = { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 119 };
    private static readonly byte[] ChromaAcHuffval =
    {
        0x00,0x01,0x02,0x03,0x11,0x04,0x05,0x21,0x31,0x06,0x12,0x41,0x51,0x07,0x61,
        0x71,0x13,0x22,0x32,0x81,0x08,0x14,0x42,0x91,0xa1,0xb1,0xc1,0x09,0x23,0x33,
        0x52,0xf0,0x15,0x62,0x72,0xd1,0x0a,0x16,0x24,0x34,0xe1,0x25,0xf1,0x17,0x18,
        0x19,0x1a,0x26,0x27,0x28,0x29,0x2a,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,
        0x45,0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,
        0x64,0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,
        0x82,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,
        0x98,0x99,0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,
        0xb5,0xb6,0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,
        0xd2,0xd3,0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,
        0xe8,0xe9,0xea,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa
    };

    #endregion

    #region Internal structures

    /// <summary>
    /// Узел дерева Хаффмана
    /// </summary>
    private sealed class HuffNode
    {
        public HuffNode? Zero;
        public HuffNode? One;
        public int Symbol = -1;
    }

    private readonly Dictionary<byte, (uint Code, int Length)>[] _encodeTables; // Таблица кодирования

    /// <summary>
    /// Дерево декодирования
    /// </summary>
    private readonly HuffNode[] _decodeTrees;

    #endregion

    #region Constructor

    /// <summary>
    /// Конструктор
    /// </summary>
    public HuffmanCoder()
    {
        _encodeTables = new Dictionary<byte, (uint, int)>[4];
        _encodeTables[0] = BuildEncodeTable(LumaDcBits, LumaDcHuffval);
        _encodeTables[1] = BuildEncodeTable(LumaAcBits, LumaAcHuffval);
        _encodeTables[2] = BuildEncodeTable(ChromaDcBits, ChromaDcHuffval);
        _encodeTables[3] = BuildEncodeTable(ChromaAcBits, ChromaAcHuffval);

        _decodeTrees = new HuffNode[4];
        _decodeTrees[0] = BuildDecodeTree(LumaDcBits, LumaDcHuffval);
        _decodeTrees[1] = BuildDecodeTree(LumaAcBits, LumaAcHuffval);
        _decodeTrees[2] = BuildDecodeTree(ChromaDcBits, ChromaDcHuffval);
        _decodeTrees[3] = BuildDecodeTree(ChromaAcBits, ChromaAcHuffval);
    }

    #endregion

    #region Encode

    /// <summary>
    /// Записывает DC-коэффициент в битовом потоке
    /// Кодирование: Хаффман(cat) + VLI(diff, cat)
    /// </summary>
    public void EncodeDc(int diff, bool isLuminance, BitWriter writer)
    {
        int cat = BitLength(diff);
        var table = _encodeTables[isLuminance ? 0 : 2];
        var (code, len) = table[(byte)cat];
        writer.WriteBits(code, len);
        if (cat > 0)
            writer.WriteBits(EncodeVli(diff, cat), cat);
    }

    /// <summary>
    /// Записывает AC-коэффициент в битовом потоке
    /// Кодирование: Хаффман(cat) + VLI(value, cat)
    /// </summary>
    public void EncodeAc(int runLength, int value, bool isLuminance, BitWriter writer)
    {
        var table = _encodeTables[isLuminance ? 1 : 3];

        if (runLength == 0 && value == 0)
        {
            var (eobCode, eobLen) = table[0x00];
            writer.WriteBits(eobCode, eobLen);
            return;
        }

        int cat = BitLength(value);
        byte symbol = (byte)((runLength << 4) | cat);

        if (!table.TryGetValue(symbol, out var entry))
            throw new InvalidOperationException($"No Huffman code for AC symbol 0x{symbol:X2}");

        writer.WriteBits(entry.Code, entry.Length);
        if (cat > 0)
            writer.WriteBits(EncodeVli(value, cat), cat);
    }

    #endregion

    #region Decode

    /// <summary>
    /// Считывает и декодирует следующий DC-символ из битового потока
    /// </summary>
    public int DecodeDc(bool isLuminance, BitReader reader)
    {
        int cat = DecodeSymbol(_decodeTrees[isLuminance ? 0 : 2], reader);
        if (cat == 0) return 0;
        int bits = reader.ReadBits(cat);
        return DecodeVli(bits, cat);
    }

    /// <summary>
    /// Считывает и декодирует следующий AC-символ из битового потока
    /// </summary>
    public (int RunLength, int Value) DecodeAc(bool isLuminance, BitReader reader)
    {
        int symbol = DecodeSymbol(_decodeTrees[isLuminance ? 1 : 3], reader);

        if (symbol == 0x00) return (0, 0);   // EOB
        if (symbol == 0xF0) return (15, 0);  // ZRL

        int runLength = (symbol >> 4) & 0xF;
        int cat = symbol & 0xF;
        if (cat == 0) return (runLength, 0);
        int bits = reader.ReadBits(cat);
        return (runLength, DecodeVli(bits, cat));
    }

    #endregion

    #region Table builders

    /// <summary>
    /// Создает таблицу двоичного кодирования на основе тех же BITS + HUFFVAL
    /// </summary>
    private static Dictionary<byte, (uint Code, int Length)>
        BuildEncodeTable(byte[] bits, byte[] huffval)
    {
        var table = new Dictionary<byte, (uint, int)>();
        uint code = 0;
        int hvIndex = 0;

        for (int len = 1; len <= 16; len++)
        {
            int count = bits[len - 1];
            for (int i = 0; i < count; i++)
            {
                table[huffval[hvIndex++]] = (code, len);
                code++;
            }
            code <<= 1;
        }

        return table;
    }

    /// <summary>
    /// Создает декодировочное дерево на основе BITS + HUFFVAL
    /// </summary>
    private static HuffNode BuildDecodeTree(byte[] bits, byte[] huffval)
    {
        var root = new HuffNode();
        uint code = 0;
        int hvIndex = 0;

        for (int len = 1; len <= 16; len++)
        {
            int count = bits[len - 1];
            for (int i = 0; i < count; i++)
            {
                InsertCode(root, code, len, huffval[hvIndex++]);
                code++;
            }
            code <<= 1;
        }

        return root;
    }

    /// <summary>
    /// Вставляет код в декодировочное дерево
    /// </summary>
    private static void InsertCode(HuffNode root, uint code, int len, byte symbol)
    {
        var node = root;
        // Traverse MSB → LSB
        for (int shift = len - 1; shift >= 0; shift--)
        {
            int bit = (int)((code >> shift) & 1);
            if (bit == 0)
            {
                node.Zero ??= new HuffNode();
                node = node.Zero;
            }
            else
            {
                node.One ??= new HuffNode();
                node = node.One;
            }
        }
        node.Symbol = symbol;
    }

    #endregion

    #region Decode helpers

    /// <summary>
    /// Декодирует один символ из битового потока
    /// </summary>
    private static int DecodeSymbol(HuffNode root, BitReader reader)
    {
        var node = root;
        while (node.Symbol == -1)
        {
            int bit = reader.ReadBit();
            node = bit == 0
                ? node.Zero ?? throw new InvalidDataException("Bad Huffman stream: missing Zero child")
                : node.One ?? throw new InvalidDataException("Bad Huffman stream: missing One child");
        }
        return node.Symbol;
    }

    #endregion

    #region VLI helpers

    /// <summary>
    /// Преобразует целое число со знаком в битовую последовательность JPEG VLI
    /// Если значение положительное → старший бит равен 0
    /// Если значение отрицательное → старший бит равен 1, то прибавьте (2^cat - 1)
    /// </summary>
    private static uint EncodeVli(int value, int cat)
    {
        if (value > 0) return (uint)value;
        return (uint)(value + (1 << cat) - 1);
    }

    /// <summary>
    /// Преобразует битовую последовательность JPEG VLI в целое число со знаком
    /// </summary>
    private static int DecodeVli(int bits, int cat)
    {
        if ((bits & (1 << (cat - 1))) != 0)
            return bits;
        return bits - (1 << cat) + 1;
    }

    /// <summary>
    /// Возвращает количество бит в двоичном представлении целого числа
    /// </summary>
    public static int BitLength(int value) =>
        value == 0 ? 0 : (int)Math.Floor(Math.Log2(Math.Abs(value))) + 1;

    #endregion

    #region Table access

    public static (byte[] Bits, byte[] Huffval) GetTableDefinition(bool isDc, bool isLuminance) =>
        (isDc, isLuminance) switch
        {
            (true, true) => (LumaDcBits, LumaDcHuffval),
            (false, true) => (LumaAcBits, LumaAcHuffval),
            (true, false) => (ChromaDcBits, ChromaDcHuffval),
            (false, false) => (ChromaAcBits, ChromaAcHuffval),
        };

    #endregion
}