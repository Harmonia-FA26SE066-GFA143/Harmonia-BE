using Harmonia.Domain.Enums;

namespace Harmonia.Application.Common.Models;

/// <summary>
/// Library facts about one song: its active materials per type, whether it is classified, and how often and when
/// last it served, counted like the song-usage report (past Published events whose Approved list holds it).
/// <see cref="SheetMusicPublicId"/> is the file of the oldest active SheetMusic material, used for the list thumbnail.
/// </summary>
public record SongStats(
    IReadOnlyDictionary<MaterialType, int> MaterialCounts, bool IsClassified, int TimesUsed, DateOnly? LastUsedDate,
    string? SheetMusicPublicId = null);
