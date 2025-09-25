using CTCare.Application.Leaves.Abstractions;
using CTCare.Infrastructure.Leave.Calculators;
using CTCare.Infrastructure.Leave.Calendar;
using CTCare.Infrastructure.Leave.Guards;
using CTCare.Infrastructure.Leave.Rules;

namespace CTCare.Api.Extensions;

public static class LeaveModuleDiServiceRegistration
{
    public static IServiceCollection RegisterLeaveServices(this IServiceCollection services)
    {
        services.AddSingleton<IHolidayProvider>(sp => new InMemoryHolidayProvider());
        services.AddScoped<IBusinessCalendarService, BusinessCalendarService>();

        // Core logic
        services.AddScoped<ILeaveSpanCalculator, LeaveSpanCalculator>();
        services.AddScoped<IDoctorsNoteRule, DoctorsNoteRule>();

        // Guards
        services.AddScoped<IOverlapGuard, OverlapGuardEf>();
        services.AddScoped<IBalanceGuard, BalanceGuardEf>();

        return services;
    }
}
