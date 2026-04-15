using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaCodec.Audio;

/// <summary>
/// Кодирует 16-bit PCM → IMA ADPCM (4 бита на сэмпл, сжатие 4:1).
/// Формат файла: [4b magic "ADPC"][4b channels][4b samplesPerChannel][nibble data...]
/// Стерео: каналы кодируются независимо, данные идут последовательно.
/// </summary>
public sealed class ImaAdpcmEncoder
{
    // 89 значений шага
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

    // Корректировка stepIndex по младшим 3 битам nibble
    // Маленький nibble (0-3) → шаг уменьшается; большой (4-7) → растёт
    private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8 };

    /// <summary>
    /// Кодирует PCM → ADPCM
    /// </summary>
    public byte[] Encode(short[] pcmSamples, int channels)
    {
        int samplesPerChannel = pcmSamples.Length / channels;

        // 1. Разбиваем на каналы
        var channelSamples = new short[channels][];
        for (int c = 0; c < channels; c++)
        {
            channelSamples[c] = new short[samplesPerChannel];
            for (int i = 0; i < samplesPerChannel; i++)
                channelSamples[c][i] = pcmSamples[i * channels + c];
        }

        // 2. Кодируем каждый канал независимо
        var channelData = new byte[channels][];
        for (int c = 0; c < channels; c++)
            channelData[c] = EncodeChannel(channelSamples[c]);

        // 3. Пишем заголовок + данные
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write("ADPC".ToCharArray());   // magic
        bw.Write(channels);               // int32
        bw.Write(samplesPerChannel);      // int32
        foreach (var data in channelData)
            bw.Write(data);

        return ms.ToArray();
    }

    /// <summary>
    /// Кодирует один канал
    /// </summary>
    private static byte[] EncodeChannel(short[] samples)
    {
        // 2 nibble на байт → нужно ceil(n/2) байт
        var output = new byte[(samples.Length + 1) / 2];
        var state = new ChannelState();

        for (int i = 0; i < samples.Length; i++)
        {
            byte nibble = EncodeNibble(samples[i], ref state);

            // Чётный сэмпл → младший nibble; нечётный → старший
            if (i % 2 == 0)
                output[i / 2] = nibble;
            else
                output[i / 2] |= (byte)(nibble << 4);
        }

        return output;
    }

    /// <summary>
    /// Кодирует один сэмпл
    /// </summary>
    private static byte EncodeNibble(short sample, ref ChannelState state)
    {
        int step = StepTable[state.StepIndex];
        int diff = sample - state.Predictor;

        byte nibble = 0;

        // Бит 3 — знак
        if (diff < 0) { nibble = 8; diff = -diff; }

        // Биты 2-0 — квантование разности в 3 бита
        if (diff >= step) { nibble |= 4; diff -= step; }
        step >>= 1;
        if (diff >= step) { nibble |= 2; diff -= step; }
        step >>= 1;
        if (diff >= step) nibble |= 1;

        // Обновляем predictor ТОЧНО ТАК ЖЕ как это делает decoder
        // Это критично для синхронизации состояний
        step = StepTable[state.StepIndex];
        int vpdiff = step >> 3;
        if ((nibble & 4) != 0) vpdiff += step;
        if ((nibble & 2) != 0) vpdiff += step >> 1;
        if ((nibble & 1) != 0) vpdiff += step >> 2;

        state.Predictor += (nibble & 8) != 0 ? -vpdiff : vpdiff;
        state.Predictor = Math.Clamp(state.Predictor, short.MinValue, short.MaxValue);
        state.StepIndex = Math.Clamp(state.StepIndex + IndexTable[nibble & 7], 0, 88);

        return nibble;
    }

    /// <summary>
    /// Состояние кодирования для одного канала: текущий predictor и stepIndex.
    /// </summary>
    private struct ChannelState
    {
        public int Predictor = 0;
        public int StepIndex = 0;
        public ChannelState() { }
    }
}
