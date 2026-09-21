using FFMpegCore;
using FFMpegCore.Enums;
using Newtonsoft.Json.Serialization;
using Roblox.Models.Assets;
using NAudio.Wave;
using NAudio.Dsp;

namespace Roblox.Services;

public class AudioService : ServiceBase, IService
{
    private const float maxDecibel = -2f;
    private const long maxAudioFileSizeBytes = 20447232;
    public static float GetPeakDbLevel(Stream mp3Stream)
    {
        mp3Stream.Position = 0;

        using (var mp3Reader = new Mp3FileReader(mp3Stream))
        using (var waveStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader))
        {
            return GetPeakDbLevel(waveStream.ToSampleProvider());
        }
    }

    private static float GetPeakDbLevel(ISampleProvider sampleProvider)
    {
        float peakSample = 0;

        int sampleRate = sampleProvider.WaveFormat.SampleRate;
        int channels = sampleProvider.WaveFormat.Channels;

        float[] sampleBuffer = new float[(int)(sampleRate * 0.1) * channels];

        int samplesRead;
        do
        {
            samplesRead = sampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);

            for (int i = 0; i < samplesRead; i++)
            {
                peakSample = Math.Max(peakSample, Math.Abs(sampleBuffer[i]));
            }
        } while (samplesRead > 0);

        if (peakSample <= 0)
            return float.NegativeInfinity;

        return 20f * MathF.Log10(peakSample);
    }

    private static async Task<float> GetPeakDbLevel(string audioFilePath, bool decodeWithFfmpeg)
    {
        if (!decodeWithFfmpeg)
        {
            try
            {
                await using var audioFileStream = File.OpenRead(audioFilePath);
                return GetPeakDbLevel(audioFileStream);
            }
            catch (Exception e)
            {
                Console.WriteLine("[warning] direct MP3 peak scan failed, retrying with ffmpeg decode: {0}", e.Message);
            }
        }

        await using var inputFileStream = File.OpenRead(audioFilePath);
        using var waveStream = await ConvertAudioToWave(inputFileStream);
        return GetWavePeakDbLevel(waveStream);
    }

    private static float GetWavePeakDbLevel(Stream waveStream)
    {
        waveStream.Position = 0;

        using var waveReader = new WaveFileReader(waveStream);
        return GetPeakDbLevel(waveReader.ToSampleProvider());
    }

    private static bool IsMp3Media(IMediaAnalysis mediaInfo, string audioFilePath)
    {
        if (IsMp3FormatName(mediaInfo.Format.FormatName))
            return true;

        if (mediaInfo.PrimaryAudioStream?.CodecName.Equals("mp3", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        if (mediaInfo.AudioStreams.Any(stream =>
                stream.CodecName.Equals("mp3", StringComparison.OrdinalIgnoreCase) ||
                stream.CodecLongName.Contains("mpeg audio layer 3", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return HasMp3FrameHeader(audioFilePath);
    }

    private static bool IsMp3FormatName(string formatName)
    {
        return formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(format => format.Equals("mp3", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasMp3FrameHeader(string audioFilePath)
    {
        using var fileStream = File.OpenRead(audioFilePath);
        Span<byte> header = stackalloc byte[10];
        if (fileStream.Read(header) < 4)
            return false;

        if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            fileStream.Position = DecodeId3SyncSafeSize(header.Slice(6, 4)) + 10;
            if (fileStream.Read(header) < 4)
                return false;
        }
        else
        {
            fileStream.Position = 0;
        }

        Span<byte> frame = stackalloc byte[4];
        if (fileStream.Read(frame) < frame.Length)
            return false;

        var hasFrameSync = frame[0] == 0xFF && (frame[1] & 0xE0) == 0xE0;
        var mpegVersion = (frame[1] >> 3) & 0x03;
        var layer = (frame[1] >> 1) & 0x03;
        var bitrate = (frame[2] >> 4) & 0x0F;
        var sampleRate = (frame[2] >> 2) & 0x03;

        return hasFrameSync &&
            mpegVersion != 0x01 &&
            layer == 0x01 &&
            bitrate is > 0 and < 0x0F &&
            sampleRate != 0x03;
    }

    private static int DecodeId3SyncSafeSize(Span<byte> sizeBytes)
    {
        return (sizeBytes[0] << 21) |
            (sizeBytes[1] << 14) |
            (sizeBytes[2] << 7) |
            sizeBytes[3];
    }
            
    public static async Task<MemoryStream> ConvertAudioToMp3(Stream inputStream)
    {
        string tempInput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
        string tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");
        try
        {
            await using (var fileStream = File.Create(tempInput))
            {
                inputStream.Seek(0, SeekOrigin.Begin);
                await inputStream.CopyToAsync(fileStream);
            }

            await FFMpegArguments
                .FromFileInput(tempInput)
                .OutputToFile(tempOutput, true, options =>
                    options
                        //.WithCustomArgument("-af alimiter=limit=-2.2:level=disabled:attack=5:release=50")
                        .WithAudioCodec("libmp3lame")
                        .WithAudioBitrate(AudioQuality.Normal))
                        
                .ProcessAsynchronously();

            var memoryStream = new MemoryStream();
            await using (var outputFileStream = File.OpenRead(tempOutput))
            {
                await outputFileStream.CopyToAsync(memoryStream);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] error converting audio to MP3: {ex.Message}\n");
            throw;
        }
        finally
        {
            File.Delete(tempInput);
            File.Delete(tempOutput);
        }
    }

    private static async Task<MemoryStream> ConvertAudioToWave(Stream inputStream)
    {
        string tempInput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
        string tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
        try
        {
            await using (var fileStream = File.Create(tempInput))
            {
                inputStream.Seek(0, SeekOrigin.Begin);
                await inputStream.CopyToAsync(fileStream);
            }

            await FFMpegArguments
                .FromFileInput(tempInput)
                .OutputToFile(tempOutput, true, options =>
                    options.WithCustomArgument("-vn -acodec pcm_f32le -f wav"))
                .ProcessAsynchronously();

            var memoryStream = new MemoryStream();
            await using (var outputFileStream = File.OpenRead(tempOutput))
            {
                await outputFileStream.CopyToAsync(memoryStream);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] error converting audio to WAV: {ex.Message}\n");
            throw;
        }
        finally
        {
            File.Delete(tempInput);
            File.Delete(tempOutput);
        }
    }
    public static async Task<MediaValidation> IsAudioValid(Stream content, long creatorId, CancellationToken cancellationToken = default)
    {
        if (content.Length > maxAudioFileSizeBytes)
            return MediaValidation.FileTooLarge;
        if (content.Length == 0)
            return MediaValidation.EmptyStream;
        content.Position = 0;
        var newStream = new StreamContent(content, 81920);
        IMediaAnalysis mediaInfo;
        // streams return an empty duration, so we have to write to disk and then read that...
        // https://github.com/rosenbjerg/FFMpegCore/issues/130#issuecomment-739572946
        var tempFile = Path.GetTempFileName();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linkedToken = linkedCts.Token;

        try
        {
            await using (var fs = File.OpenWrite(tempFile))
            {
                await newStream.CopyToAsync(fs, linkedToken);
            }

            mediaInfo = await FFProbe.AnalyseAsync(tempFile, cancellationToken: linkedToken);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);

            return MediaValidation.UnsupportedFormat;
        }
        catch (Exception e)
        {
            Console.WriteLine("[error] error validating audio: {0}\n{1}", e.Message, e.StackTrace);
            if (File.Exists(tempFile))
                File.Delete(tempFile);

            return MediaValidation.UnsupportedFormat;
        }

        try
        {
            if (mediaInfo.Duration > TimeSpan.FromMinutes(7))
                return MediaValidation.TooLong;

            if (mediaInfo.Duration < TimeSpan.FromMilliseconds(10))
                return MediaValidation.TooShort;

            var formatDetails = mediaInfo.Format;

            var isMp3 = IsMp3Media(mediaInfo, tempFile);
            var isCreatorFormatException = creatorId == 15422 ||
                creatorId == 16815 ||
                creatorId == 16024 ||
                creatorId == 13224;

            if (!isMp3 && !isCreatorFormatException)
                return MediaValidation.UnsupportedFormat;

            try
            {
                var peakDb = await GetPeakDbLevel(tempFile, !isMp3);
                if (peakDb > maxDecibel)
                    return MediaValidation.TooLoud;
            }
            catch (Exception e)
            {
                Console.WriteLine("[error] error checking audio peak level: {0}\n{1}", e.Message, e.StackTrace);
                return MediaValidation.UnsupportedFormat;
            }

            return MediaValidation.Ok;
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }
}
