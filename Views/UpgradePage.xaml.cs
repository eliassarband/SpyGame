using SpyGame.Models;
using SpyGame.Services;

namespace SpyGame.Views;

public partial class UpgradePage : ContentPage
{
    private readonly PremiumManager _premium;
    private readonly IBillingService _billing;

    private int _titleTapCount;

    public UpgradePage(PremiumManager premium, IBillingService billing)
    {
        InitializeComponent();
        _premium = premium;
        _billing = billing;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _titleTapCount = 0;
        DevSection.IsVisible = false;
        RefreshStatus();
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
            BuyButton.Text = "خرید از مایکت";
            BuyButton.IsEnabled = true;
        }
    }

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
        BuyButton.IsEnabled = false;
        BuyButton.Text = "در حال اتصال به مایکت...";

        var result = await _billing.LaunchPurchaseAsync("premium_unlock");

        switch (result.Code)
        {
            case BillingResultCode.Ok:
                RefreshStatus();
                await DisplayAlert("خرید موفق 🎉", "بسته ویژه فعال شد!\nاز پشتیبانی شما ممنونیم.", "عالیه");
                break;

            case BillingResultCode.UserCancelled:
                RefreshStatus(); // re-enables the button
                break;

            case BillingResultCode.ItemAlreadyOwned:
                await _premium.UnlockAsync(PremiumFeature.Premium);
                RefreshStatus();
                await DisplayAlert("قبلاً خریداری شده", "بسته ویژه قبلاً خریداری شده و اکنون فعال است.", "باشه");
                break;

            default:
                RefreshStatus();
                await DisplayAlert("خطا", result.Message ?? "خطای ناشناخته. لطفاً دوباره امتحان کنید.", "باشه");
                break;
        }
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
