using SpyGame.Data;

namespace SpyGame.Views;

public partial class SplashPage : ContentPage
{
    private readonly AppDatabase _db;

    public SplashPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ورود کارت
        if (Card != null)
        {
            Card.Opacity = 0;
            Card.Scale = 0.98;
            await Task.WhenAll(
                Card.FadeTo(1, 220, Easing.CubicOut),
                Card.ScaleTo(1, 260, Easing.CubicOut)
            );
        }

        // پالس نرم لوگو (۲ بار)
        if (Logo != null)
        {
            for (int i = 0; i < 3; i++)
            {
                await Logo.ScaleTo(1.06, 320, Easing.CubicOut);
                await Logo.ScaleTo(1.00, 280, Easing.CubicIn);
            }
        }

        // Seed دیتابیس
        await _db.InitAsync();

        // مکث کوتاه برای حس اسپلش
        await Task.Delay(800);

        bool tutorialShown = Preferences.Get("tutorial_shown", false);
        if (!tutorialShown)
            await Shell.Current.GoToAsync(nameof(TutorialPage));
        else
            await Shell.Current.GoToAsync(nameof(SetupPage));
    }
}
