namespace SpyGame;

using SpyGame.Views;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // ثبت مسیرها برای ناوبری با Shell
        Routing.RegisterRoute(nameof(SetupPage), typeof(SetupPage));
        Routing.RegisterRoute(nameof(RevealPage), typeof(RevealPage));
        Routing.RegisterRoute(nameof(TimerPage), typeof(TimerPage));
        Routing.RegisterRoute(nameof(ResultPage), typeof(ResultPage));
        Routing.RegisterRoute(nameof(TutorialPage), typeof(TutorialPage));
        Routing.RegisterRoute(nameof(StatisticsPage), typeof(StatisticsPage));
        Routing.RegisterRoute(nameof(CustomWordsPage), typeof(CustomWordsPage));
        Routing.RegisterRoute(nameof(UpgradePage), typeof(UpgradePage));
    }
}
