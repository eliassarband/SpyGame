using SpyGame.Data;

namespace SpyGame.Views;

public partial class SplashPage : ContentPage
{
    private readonly AppDatabase _db;

    // ⬅️ DI
    public SplashPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Seed دیتابیس قبل از نمایش Setup
        await _db.InitAsync();

        await Task.Delay(1800);
        await Shell.Current.GoToAsync(nameof(SetupPage));
    }
}
