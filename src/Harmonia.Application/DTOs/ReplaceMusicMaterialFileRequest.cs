namespace Harmonia.Application.DTOs;

/// <summary>
/// Form fields sent with the new file of a material. The material keeps its id, title, type, target skill and
/// assignments; <see cref="ResetProgress"/> clears what members marked when the content really changed.
/// </summary>
public class ReplaceMusicMaterialFileRequest
{
    public bool ResetProgress { get; set; }
}
