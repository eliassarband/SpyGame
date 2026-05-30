using Microsoft.Extensions.Logging;
using SpyGame.Data;
using SpyGame.Services;
using SpyGame.Views;

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

            // SessionManager + PremiumManager: Singleton
            builder.Services.AddSingleton<SessionManager>();
            builder.Services.AddSingleton<PremiumManager>();

            // Billing: فقط سرویس مارکت هدف ثبت می‌شود
#if ANDROID && MARKET_MYKET
            builder.Services.AddSingleton<IBillingService, SpyGame.Platforms.Android.MyketBillingService>();
#elif ANDROID && MARKET_BAZAAR
            builder.Services.AddSingleton<IBillingService, SpyGame.Platforms.Android.BazaarBillingService>();
#else
            builder.Services.AddSingleton<IBillingService, NullBillingService>();
#endif

            // DI: صفحات
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<SetupPage>();
            builder.Services.AddTransient<RevealPage>();
            builder.Services.AddTransient<TimerPage>();
            builder.Services.AddTransient<ResultPage>();
            builder.Services.AddTransient<TutorialPage>();
            builder.Services.AddTransient<StatisticsPage>();
            builder.Services.AddTransient<CustomWordsPage>();
            builder.Services.AddTransient<UpgradePage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
