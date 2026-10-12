using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Services;

/// <summary>
/// Which columns each exported report has. Titles are Vietnamese on purpose: the file goes straight to the
/// Admin with no client in between to translate them (02-naming.md). Keys are the JSON field names of the
/// report endpoints, so the XML and JSON exports line up with the API.
/// </summary>
public static class ReportExportColumns
{
    public static ReportTable UserActivity(IEnumerable<UserActivityDto> rows) => Table(
        "Hoạt động tài khoản", rows,
        ("fullName", "Họ tên", x => x.FullName),
        ("email", "Email", x => x.Email),
        ("roleName", "Vai trò", x => x.RoleName),
        ("isActive", "Đang hoạt động", x => x.IsActive ? "Có" : "Không"),
        ("lastLoginAt", "Đăng nhập gần nhất", x => x.LastLoginAt is { } at ? VietnamTime.FromUtc(at) : null),
        ("passwordChangeCount", "Số lần đổi mật khẩu", x => x.PasswordChangeCount),
        ("passwordResetCount", "Số lần đặt lại mật khẩu", x => x.PasswordResetCount),
        ("accountChangeCount", "Số lần sửa tài khoản", x => x.AccountChangeCount),
        ("actionCount", "Số thao tác", x => x.ActionCount));

    public static ReportTable RehearsalAttendance(IEnumerable<RehearsalAttendanceReportDto> rows) => Table(
        "Điểm danh buổi tập", rows,
        ("fullName", "Họ tên", x => x.FullName),
        ("presentCount", "Có mặt", x => x.PresentCount),
        ("lateCount", "Đi trễ", x => x.LateCount),
        ("absentCount", "Vắng", x => x.AbsentCount),
        ("excusedCount", "Vắng có phép", x => x.ExcusedCount));

    public static ReportTable Participation(IEnumerable<ParticipationReportDto> rows) => Table(
        "Xác nhận tham gia", rows,
        ("fullName", "Họ tên", x => x.FullName),
        ("invitedCount", "Chưa trả lời", x => x.InvitedCount),
        ("confirmedCount", "Xác nhận", x => x.ConfirmedCount),
        ("declinedCount", "Từ chối", x => x.DeclinedCount),
        ("unsureCount", "Chưa chắc", x => x.UnsureCount));

    public static ReportTable AssignmentCompletion(IEnumerable<AssignmentCompletionDto> rows) => Table(
        "Hoàn thành bài tập", rows,
        ("fullName", "Họ tên", x => x.FullName),
        ("assignedCount", "Được giao", x => x.AssignedCount),
        ("passedCount", "Đạt", x => x.PassedCount),
        ("needsRevisionCount", "Cần chỉnh sửa", x => x.NeedsRevisionCount),
        ("awaitingReviewCount", "Chờ nhận xét", x => x.AwaitingReviewCount),
        ("notSubmittedCount", "Chưa nộp", x => x.NotSubmittedCount));

    public static ReportTable SongUsage(IEnumerable<SongUsageDto> rows) => Table(
        "Tần suất sử dụng bài hát", rows,
        ("title", "Bài hát", x => x.Title),
        ("composer", "Tác giả", x => x.Composer),
        ("timesUsed", "Số lần dùng", x => x.TimesUsed),
        ("lastUsedDate", "Lần dùng gần nhất", x => x.LastUsedDate));

    public static ReportTable ServiceHistory(IEnumerable<ServiceHistoryDto> rows) => Table(
        "Lịch sử phục vụ", rows,
        ("eventDate", "Ngày", x => x.EventDate),
        ("time", "Giờ", x => x.Time),
        ("title", "Sự kiện", x => x.Title),
        ("liturgicalSeasonName", "Mùa phụng vụ", x => x.LiturgicalSeasonName),
        ("locationName", "Địa điểm", x => x.LocationName),
        ("songCount", "Số bài hát", x => x.SongCount),
        ("membersServed", "Số ca viên phục vụ", x => x.MembersServed),
        ("participationConfirmed", "Xác nhận tham gia", x => x.ParticipationConfirmed),
        ("participationDeclined", "Từ chối tham gia", x => x.ParticipationDeclined),
        ("rehearsalsHeld", "Số buổi tập", x => x.RehearsalsHeld),
        ("attendancePresent", "Lượt có mặt", x => x.AttendancePresent));

    private static ReportTable Table<T>(
        string title, IEnumerable<T> rows, params (string Key, string Title, Func<T, object?> Value)[] columns) =>
        new(
            title,
            columns.Select(c => (c.Key, c.Title)).ToList(),
            rows.Select(row => (IReadOnlyList<object?>)columns.Select(c => c.Value(row)).ToList()).ToList());
}
