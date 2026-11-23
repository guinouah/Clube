using Clubes.Mobile.Core.Services;
using Clubes.Mobile.Core.ViewModels;
using Clubes.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clubes.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var apiBaseAddress =
#if ANDROID
            "http://10.0.2.2:5140/";
#else
            "http://localhost:5140/";
#endif
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(apiBaseAddress),
            Timeout = TimeSpan.FromSeconds(8)
        });
        builder.Services.AddSingleton(_ => new OfflineStore(Path.Combine(FileSystem.AppDataDirectory, "clubes-local.db3")));
        builder.Services.AddSingleton<IClubesApi, ClubesApiClient>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<EventsViewModel>();
        builder.Services.AddTransient<DuesViewModel>();
        builder.Services.AddSingleton<OfflineSyncCoordinator>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<EventsPage>();
        builder.Services.AddTransient<DuesPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
