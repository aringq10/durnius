using Durnius.Web.Engines.Karas;

namespace Durnius.Web.Features.Multiplayer;

public static class MultiplayerServiceExtensions
{
    public static IServiceCollection AddMultiplayer(this IServiceCollection services)
    {
        var registry = new MatchRegistry()
            .Register<KarasMatch>("karas");

        services.AddSingleton<SessionStore>();
        services.AddSingleton<LobbyManager>();
        services.AddSingleton<MatchManager>();
        services.AddSingleton<MultiplayerCoordinator>();
        services.AddSingleton(registry);
        services.AddScoped<MatchHistoryRecorder>();
        services.AddSignalR();

        return services;
    }
}
