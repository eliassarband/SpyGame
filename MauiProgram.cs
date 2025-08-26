using Microsoft.Extensions.Logging;
using SpyGame.Data;
using SpyGame.Views; // ⬅️ برای رجیستر صفحات

namespace SpyGame
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // ⬇️ بسیار مهم: راه‌اندازی SQLitePCLRaw
            SQLitePCL.Batteries_V2.Init();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // DB به صورت Singleton
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "spygame.db3");
            builder.Services.AddSingleton(new AppDatabase(dbPath));

            // DI: صفحات
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<SetupPage>();
            builder.Services.AddTransient<RevealPage>();
            builder.Services.AddTransient<TimerPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
