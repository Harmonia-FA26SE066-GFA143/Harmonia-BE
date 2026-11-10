using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Reads the length from the file headers with TagLib#, in memory: nothing touches the disk and the stream is not closed.
/// </summary>
public class AudioDurationReader : IAudioDurationReader
{
    public Result<double> ReadSeconds(Stream audio, string fileName)
    {
        try
        {
            using var file = TagLib.File.Create(new StreamAbstraction(fileName, audio));
            var seconds = file.Properties?.Duration.TotalSeconds ?? 0;
            return seconds > 0 && double.IsFinite(seconds)
                ? Result<double>.Success(Math.Round(seconds, 3))
                : Result<double>.Failure(ErrorCodes.MaterialAudioInvalid);
        }
        // An untrusted file in a third-party parser: any parse failure is a bad upload, not a server error.
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return Result<double>.Failure(ErrorCodes.MaterialAudioInvalid);
        }
    }

    // TagLib picks the format from the extension of Name; CloseStream is a no-op so the caller keeps its stream.
    private sealed class StreamAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name => name;

        public Stream ReadStream => stream;

        public Stream WriteStream => stream;

        public void CloseStream(Stream stream)
        {
        }
    }
}
