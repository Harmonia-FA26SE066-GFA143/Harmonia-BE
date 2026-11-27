using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class RehearsalAttendanceAlreadyRecordedException() : DomainException(ErrorCodes.RehearsalAttendanceAlreadyRecorded, "Attendance has already been recorded for this member");
