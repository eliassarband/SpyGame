namespace SpyGame;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // تبعیت از تم سیستم
        UserAppTheme = AppTheme.Dark;
        MainPage = new AppShell(); // یا NavigationPage(...)
    }
}
