using SpyGame.Models;
using SpyGame.Services;

namespace SpyGame.Views;

public partial class UpgradePage : ContentPage
{
    private readonly PremiumManager _premium;

    // تعداد ضربه روی عنوان برای نمایش بخش dev (در production قابل نگه‌داشتن)
    private int _titleTapCount;

    public UpgradePage(PremiumManager premium)
    {
        InitializeComponent();
        _premium = premium;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _titleTapCount = 0;
        RefreshStatus();

        // بخش Dev فقط با ۷ ضربه روی عنوان نمایان می‌شه
        DevSection.IsVisible = false;
    }

    private void RefreshStatus()
    {
        bool isPremium = _premium.IsUnlocked(PremiumFeature.Premium);

        if (isPremium)
        {
            StatusLabel.Text = "✅ بسته ویژه فعال است";
            StatusLabel.TextColor = Color.FromArgb("#4CAF50");
            StatusLabel.IsVisible = true;
            BuyButton.Text = "بسته ویژه فعال است ✅";
            BuyButton.IsEnabled = false;
        }
        else
        {
            StatusLabel.IsVisible = false;
            BuyButton.Text = "خرید از بازار";
            BuyButton.IsEnabled = true;
        }
    }

    // ضربه روی عنوان برای نمایش بخش dev (۷ بار)
    private void OnTitleTapped(object sender, TappedEventArgs e)
    {
        _titleTapCount++;
        if (_titleTapCount >= 7)
        {
            DevSection.IsVisible = true;
            _titleTapCount = 0;
        }
    }

    private async void OnBuyClicked(object sender, EventArgs e)
    {
        // ===================================================
        // اینجا SDK بازار یا مایکت صدا زده می‌شه
        // فعلاً: راهنمای موقت نمایش داده می‌شه
        // ===================================================
        await DisplayAlert(
            "خرید از بازار",
            "اتصال به درگاه پرداخت بازار به زودی فعال می‌شه.\n\n" +
            "برای اطلاع از زمان انتشار، اپ را دنبال کنید.",
            "باشه");
    }

    private async void OnBack(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SetupPage));

    // ---- Dev helpers ----

    private async void OnDevUnlock(object sender, EventArgs e)
    {
        await _premium.UnlockAsync(PremiumFeature.Premium);
        RefreshStatus();
        await DisplayAlert("Dev", "بسته ویژه آزمایشی فعال شد.", "باشه");
    }

    private void OnDevLock(object sender, EventArgs e)
    {
        _premium.DevLock();
        RefreshStatus();
    }
}
