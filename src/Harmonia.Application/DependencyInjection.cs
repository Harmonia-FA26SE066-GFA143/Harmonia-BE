using FluentValidation;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddAutoMapper(cfg => { }, typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMemberProfileService, MemberProfileService>();
        services.AddScoped<IMemberSkillService, MemberSkillService>();
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IMusicMaterialService, MusicMaterialService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<ISongPersonnelRequirementService, SongPersonnelRequirementService>();
        services.AddScoped<IRosterService, RosterService>();
        services.AddScoped<ILiturgicalDayService, LiturgicalDayService>();
        services.AddScoped<IUpcomingScheduleService, UpcomingScheduleService>();
        services.AddScoped<ILiturgicalEventService, LiturgicalEventService>();
        services.AddScoped<IPracticeAssignmentService, PracticeAssignmentService>();
        services.AddScoped<IPracticeSubmissionService, PracticeSubmissionService>();
        services.AddScoped<IRehearsalAttendanceService, RehearsalAttendanceService>();

        return services;
    }
}
