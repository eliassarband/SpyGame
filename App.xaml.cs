namespace SpyGame;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        string savedTheme = Preferences.Get("app_theme", "روشن");
        UserAppTheme = savedTheme switch
        {
            "تیره" => AppTheme.Dark,
            "طبق گوشی" => AppTheme.Unspecified,
            _ => AppTheme.Light
        };
        MainPage = new AppShell(); // یا NavigationPage(...)
    }
}
