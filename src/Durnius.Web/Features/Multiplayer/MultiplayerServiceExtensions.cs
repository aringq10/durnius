namespace Durnius.Web.Features.Multiplayer;

public static class MultiplayerServiceExtensions
{
    public static IServiceCollection AddMultiplayer(this IServiceCollection services)
    {
        services.AddSingleton<LobbyManager>();
        services.AddSingleton<MatchManager>();
        services.AddSignalR();

        return services;
    }
}
