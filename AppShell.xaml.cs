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
    }
}
