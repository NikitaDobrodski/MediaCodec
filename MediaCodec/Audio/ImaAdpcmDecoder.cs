using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Audio;

/// <summary>
/// Декодирует IMA ADPCM поток обратно в 16-bit PCM
/// Зеркало ImaAdpcmEncoder, таблицы и логика идентичны
/// </summary>
public sealed class ImaAdpcmDecoder
{
    private static readonly int[] StepTable =
    {
           7,    8,    9,   10,   11,   12,   13,   14,   16,   17,
          19,   21,   23,   25,   28,   31,   34,   37,   41,   45,
          50,   55,   60,   66,   73,   80,   88,   97,  107,  118,
         130,  143,  157,  173,  190,  209,  230,  253,  279,  307,
         337,  371,  408,  449,  494,  544,  598,  658,  724,  796,
         876,  963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
        2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
        5894, 6484, 7132, 7845, 8630, 9493,10442,11487,12635,13899,
       15289,16818,18500,20350,22385,24623,27086,29794,32767
    };

    private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8 };

    /// <summary>
    /// Декодирует ADPCM поток
    /// </summary>
    public short[] Decode(byte[] adpcmData, int channels)
    {
        using var ms = new MemoryStream(adpcmData);
        using var br = new BinaryReader(ms);

        // Читаем заголовок
        var magic = new string(br.ReadChars(4));
        if (magic != "ADPC")
            throw new InvalidDataException($"Expected ADPC magic, got '{magic}'.");

        int ch = br.ReadInt32();
        int samplesPerChannel = br.ReadInt32();

        if (ch != channels)
            throw new InvalidDataException($"File has {ch} channels, expected {channels}.");

        // Декодируем каждый канал
        var channelSamples = new short[ch][];
        for (int c = 0; c < ch; c++)
        {
            int byteCount = (samplesPerChannel + 1) / 2;
            var data = br.ReadBytes(byteCount);
            channelSamples[c] = DecodeChannel(data, samplesPerChannel);
        }

        // Восстанавливаем интерливинг: [L0 R0 L1 R1 ...]
        var output = new short[samplesPerChannel * ch];
        for (int i = 0; i < samplesPerChannel; i++)
            for (int c = 0; c < ch; c++)
                output[i * ch + c] = channelSamples[c][i];

        return output;
    }

    /// <summary>
    /// Декодирует один канал
    /// </summary>
    private static short[] DecodeChannel(byte[] data, int sampleCount)
    {
        var output = new short[sampleCount];
        var state = new ChannelState();

        for (int i = 0; i < sampleCount; i++)
        {
            // Чётный → младший nibble, нечётный → старший
            int nibble = i % 2 == 0
                ? (data[i / 2] & 0x0F)
                : (data[i / 2] >> 4);

            output[i] = DecodeNibble(nibble, ref state);
        }

        return output;
    }

    /// <summary>
    /// Декодирует один сэмпл
    /// </summary>
    private static short DecodeNibble(int nibble, ref ChannelState state)
    {
        int step = StepTable[state.StepIndex];
        int vpdiff = step >> 3;

        if ((nibble & 4) != 0) vpdiff += step;
        if ((nibble & 2) != 0) vpdiff += step >> 1;
        if ((nibble & 1) != 0) vpdiff += step >> 2;

        state.Predictor += (nibble & 8) != 0 ? -vpdiff : vpdiff;
        state.Predictor = Math.Clamp(state.Predictor, short.MinValue, short.MaxValue);
        state.StepIndex = Math.Clamp(state.StepIndex + IndexTable[nibble & 7], 0, 88);

        return (short)state.Predictor;
    }

    /// <summary>
    /// Состояние одного канала: текущий предсказатель и индекс шага
    /// </summary>
    private struct ChannelState
    {
        public int Predictor = 0;
        public int StepIndex = 0;
        public ChannelState() { }
    }
}
