namespace Harmonia.Application.DTOs;

/// <summary>
/// An uploaded file handed from the controller to a service without depending on ASP.NET's IFormFile.
/// <see cref="Length"/> must come from the server-side upload, never from a client-sent field.
/// </summary>
public record FileContent(Stream Content, string FileName, long Length);
