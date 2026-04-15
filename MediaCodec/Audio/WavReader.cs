using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Audio;

public sealed class WavReader
{
    public int SampleRate { get; private init; }
    public int Channels { get; private init; }
    public int BitsPerSample { get; private init; }
    public short[] Samples { get; private init; } = Array.Empty<short>();

    private WavReader() { }

    /// <summary>
    /// Читает WAV-файл (16-bit или 24-bit PCM, моно/стерео)
    /// </summary>
    public static WavReader Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        // RIFF chunk
        if (new string(br.ReadChars(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file.");
        br.ReadInt32();
        if (new string(br.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");

        // fmt chunk
        SkipToChunk(br, "fmt ");
        int fmtSize = br.ReadInt32();
        short audioFmt = br.ReadInt16();
        short channels = br.ReadInt16();
        int sampleRate = br.ReadInt32();
        br.ReadInt32();                          // byte rate
        br.ReadInt16();                          // block align
        short bits = br.ReadInt16();

        if (audioFmt != 1)
            throw new NotSupportedException($"Only PCM WAV supported (got format {audioFmt}).");
        if (bits != 16 && bits != 24)
            throw new NotSupportedException($"Only 16/24-bit WAV supported (got {bits}-bit).");

        if (fmtSize > 16) br.ReadBytes(fmtSize - 16);

        // data chunk
        SkipToChunk(br, "data");
        int dataBytes = br.ReadInt32();
        int bytesPerSample = bits / 8;
        int sampleCount = dataBytes / bytesPerSample;
        var samples = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            if (bits == 16)
            {
                samples[i] = br.ReadInt16();
            }
            else // 24-bit → конвертируем в 16-bit, выбрасываем младший байт
            {
                byte b0 = br.ReadByte();
                byte b1 = br.ReadByte();
                byte b2 = br.ReadByte();
                int s24 = (b2 << 16) | (b1 << 8) | b0;
                if ((s24 & 0x800000) != 0)
                    s24 |= unchecked((int)0xFF000000); // sign extend до 32-bit
                samples[i] = (short)(s24 >> 8);
            }
        }

        return new WavReader
        {
            SampleRate = sampleRate,
            Channels = channels,
            BitsPerSample = bits,
            Samples = samples
        };
    }

    /// <summary>
    /// Сохраняет 16-bit PCM WAV-файл
    /// </summary>
    public static void Save(string path, short[] samples, int sampleRate, int channels)
    {
        int dataSize = samples.Length * 2;

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataSize);
        bw.Write("WAVE".ToCharArray());

        bw.Write("fmt ".ToCharArray());
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * 2);
        bw.Write((short)(channels * 2));
        bw.Write((short)16);

        bw.Write("data".ToCharArray());
        bw.Write(dataSize);
        foreach (var s in samples) bw.Write(s);
    }

    private static void SkipToChunk(BinaryReader br, string id)
    {
        while (true)
        {
            var found = new string(br.ReadChars(4));
            if (found == id) return;
            int size = br.ReadInt32();
            br.ReadBytes(size);
        }
    }
}