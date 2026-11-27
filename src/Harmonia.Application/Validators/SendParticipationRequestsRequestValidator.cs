using FluentValidation;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Validators;

public class SendParticipationRequestsRequestValidator : AbstractValidator<SendParticipationRequestsRequest>
{
    public SendParticipationRequestsRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();

        RuleForEach(x => x.MemberIds)
            .NotEmpty()
            .When(x => x.MemberIds is not null);
    }
}