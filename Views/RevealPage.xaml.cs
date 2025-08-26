using Plugin.Maui.Audio;
using Microsoft.Maui.ApplicationModel;
using SpyGame.Models;
using SpyGame.Data;

namespace SpyGame.Views;

public partial class RevealPage : ContentPage
{
    private readonly AppDatabase _db;

    private bool _isRevealed = false;
    private IAudioPlayer? _shortBeep;
    private bool _buttonLocked = false;

    // DI سازنده
    public RevealPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    //// سازندهٔ بدون پارامتر برای Shell (از DI می‌گیرد)
    //public RevealPage() : this(App.Current.Services.GetRequiredService<AppDatabase>()) { }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // مخفی کردن فلش Back (اضافی در کنار XAML)
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false, IsEnabled = false });

        // آماده‌سازی بوق کوتاه
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("beep_short.wav");
            _shortBeep = AudioManager.Current.CreatePlayer(stream);
        }
        catch { _shortBeep = null; }

        await UpdateHeaderAsync();
        ResetButton();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { _shortBeep?.Dispose(); } catch { }
        _shortBeep = null;
    }

    // دکمه Back سخت‌افزاری/سیستمی کار نکند
    protected override bool OnBackButtonPressed() => true;

    private async Task UpdateHeaderAsync()
    {
        var config = await _db.GetLastConfigAsync();
        if (config == null) return;

        PlayerIndexLabel.Text = $"بازیکن {config.CurrentPlayerIndex + 1} از {config.Players}";
        WordLabel.Text = string.Empty;
    }

    private void ResetButton()
    {
        _isRevealed = false;
        _buttonLocked = false;
        RevealButton.IsEnabled = true;
        RevealButton.Text = "کلیک کنید";
        WordLabel.Text = string.Empty;
    }

    private async void OnRevealClicked(object sender, EventArgs e)
    {
        if (_buttonLocked) return;

        var config = await _db.GetLastConfigAsync();
        if (config == null) return;

        int i = config.CurrentPlayerIndex;
        bool isSpy = config.SpyIndices.Contains(i);

        if (!_isRevealed)
        {
            _isRevealed = true;
            RevealButton.Text = "گوشی را بده نفر بعد";
            WordLabel.Text = isSpy ? "جاسوس شدی :)" : $"کلمه: {config.SecretWord}";
        }
        else
        {
            _buttonLocked = true;
            RevealButton.IsEnabled = false;

            try { _shortBeep?.Play(); } catch { }
            try { Vibration.Vibrate(TimeSpan.FromMilliseconds(40)); } catch { }

            config.CurrentPlayerIndex++;
            await _db.UpdateGameConfigAsync(config);

            if (config.CurrentPlayerIndex >= config.Players)
            {
                // شروع تایمر – Root nav برای تمیز شدن history
                await Shell.Current.GoToAsync($"{nameof(TimerPage)}");
                return;
            }

            await UpdateHeaderAsync();
            ResetButton();
        }
    }

    private async void OnBackToSetup(object sender, EventArgs e)
    {
        try { _shortBeep?.Dispose(); } catch { }
        await Shell.Current.GoToAsync($"{nameof(SetupPage)}");
    }
}
