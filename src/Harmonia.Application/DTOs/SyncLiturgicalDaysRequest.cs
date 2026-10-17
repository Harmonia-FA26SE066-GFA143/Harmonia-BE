namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/liturgical-days/sync: the civil year to fetch from the liturgical calendar.</summary>
public class SyncLiturgicalDaysRequest
{
    public int Year { get; set; }
}
