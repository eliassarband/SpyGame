namespace SpyGame;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // تبعیت از تم سیستم
        UserAppTheme = AppTheme.Unspecified;
        MainPage = new AppShell(); // یا NavigationPage(...)
    }
}
