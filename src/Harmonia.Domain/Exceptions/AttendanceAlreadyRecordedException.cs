using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class AttendanceAlreadyRecordedException() : DomainException(ErrorCodes.AttendanceAlreadyRecorded, "Attendance has already been recorded for this member");
