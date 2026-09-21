using Harmonia.Domain.Common;

namespace Harmonia.Domain.Exceptions;

public class SettingValueTypeMismatchException() : DomainException(ErrorCodes.SettingValueTypeMismatch, "Setting value does not match its declared data type");
