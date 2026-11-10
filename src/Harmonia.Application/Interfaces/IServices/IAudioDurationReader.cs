using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Reads the length of an uploaded audio file from its headers; <c>fileName</c> picks the format by extension.
/// Leaves the stream wherever it read to: the caller rewinds. MATERIAL_AUDIO_INVALID.
/// </summary>
public interface IAudioDurationReader
{
    Result<double> ReadSeconds(Stream audio, string fileName);
}
