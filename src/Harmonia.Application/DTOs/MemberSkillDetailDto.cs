namespace Harmonia.Application.DTOs;

/// <summary>A declared skill together with the member who declared it, as the Choir Director reviews it.</summary>
public class MemberSkillDetailDto : MemberSkillDto
{
    public Guid MemberId { get; set; }

    public string MemberFullName { get; set; } = string.Empty;
}
