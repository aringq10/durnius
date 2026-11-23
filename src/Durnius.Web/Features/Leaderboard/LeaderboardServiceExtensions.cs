namespace Durnius.Web.Features.Leaderboard;

public static class LeaderboardServiceExtensions
{
    public static IServiceCollection AddLeaderboard(this IServiceCollection services)
    {
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddHostedService<DailyLeaderboardSettlementService>();

        return services;
    }
}
