using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Validators;

public class ExportReportRequestValidator : AbstractValidator<ExportReportRequest>
{
    public ExportReportRequestValidator()
    {
        Include(new SearchReportsRequestValidator());

        RuleFor(x => x.Type)
            .NotNull().WithErrorCode(ErrorCodes.ReportTypeNotSupported)
            .IsInEnum().WithErrorCode(ErrorCodes.ReportTypeNotSupported);
        RuleFor(x => x.Format).IsInEnum();
    }
}
