using SpyGame.Models;
using SpyGame.Services;

namespace SpyGame.Views;

public partial class UpgradePage : ContentPage
{
    private readonly PremiumManager _premium;
    private readonly IBillingService? _myket;
    private readonly IBillingService? _bazaar;

    private int _titleTapCount;

    public UpgradePage(PremiumManager premium, IEnumerable<IBillingService> billingServices)
    {
        InitializeComponent();
        _premium = premium;
        var services = billingServices.ToList();
        _myket = services.FirstOrDefault(s => s.MarketName == "Myket");
        _bazaar = services.FirstOrDefault(s => s.MarketName == "Bazaar");
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
            BuyMyketButton.Text = "بسته ویژه فعال است ✅";
            BuyMyketButton.IsEnabled = false;
            BuyBazaarButton.IsEnabled = false;
            BuyBazaarButton.Text = "بسته ویژه فعال است ✅";
        }
        else
        {
            StatusLabel.IsVisible = false;
            BuyMyketButton.Text = "خرید از مایکت";
            BuyMyketButton.IsEnabled = true;
            BuyBazaarButton.Text = "خرید از بازار";
            BuyBazaarButton.IsEnabled = true;
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

    private async void OnBuyMyketClicked(object sender, EventArgs e) =>
        await DoPurchase(_myket, BuyMyketButton, "مایکت");

    private async void OnBuyBazaarClicked(object sender, EventArgs e) =>
        await DoPurchase(_bazaar, BuyBazaarButton, "بازار");

    private async Task DoPurchase(IBillingService? service, Button button, string marketLabel)
    {
        if (service == null)
        {
            await DisplayAlert("خطا", $"سرویس {marketLabel} در دسترس نیست.", "باشه");
            return;
        }

        button.IsEnabled = false;
        button.Text = $"در حال اتصال به {marketLabel}...";

        var result = await service.LaunchPurchaseAsync("premium_unlock");

        switch (result.Code)
        {
            case BillingResultCode.Ok:
                RefreshStatus();
                await DisplayAlert("خرید موفق 🎉",
                    "بسته ویژه فعال شد!\nاز پشتیبانی شما ممنونیم.", "عالیه");
                break;

            case BillingResultCode.UserCancelled:
                RefreshStatus();
                break;

            case BillingResultCode.ItemAlreadyOwned:
                await service.LaunchPurchaseAsync("premium_unlock"); // restore
                await _premium.UnlockAsync(PremiumFeature.Premium);
                RefreshStatus();
                await DisplayAlert("قبلاً خریداری شده",
                    "بسته ویژه قبلاً خریداری شده و اکنون فعال است.", "باشه");
                break;

            case BillingResultCode.DeveloperError:
                RefreshStatus();
                await DisplayAlert("خطا",
                    $"اپ هنوز در {marketLabel} راه‌اندازی نشده یا محصول تعریف نشده.\n(کد ۵ — Developer Error)",
                    "باشه");
                break;

            case BillingResultCode.BillingUnavailable:
                RefreshStatus();
                await DisplayAlert("خطا", result.Message ?? $"{marketLabel} در دسترس نیست.", "باشه");
                break;

            default:
                RefreshStatus();
                await DisplayAlert("خطا",
                    result.Message ?? "خطای ناشناخته. لطفاً دوباره امتحان کنید.", "باشه");
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
