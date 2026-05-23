using Plugin.Maui.Audio;
using Microsoft.Maui.ApplicationModel;
using SpyGame.Data;
using SpyGame.Models;

namespace SpyGame.Views;

public partial class RevealPage : ContentPage
{
    private readonly AppDatabase _db;

    private bool _isBackSide = false;   // الان پشت نمایش داده می‌شود؟
    private bool _buttonLocked = false; // جلوگیری از کلیک‌های سریع
    private IAudioPlayer? _shortBeep;

    public RevealPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false, IsEnabled = false });

        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("beep_short.wav");
            _shortBeep = AudioManager.Current.CreatePlayer(stream);
        }
        catch { _shortBeep = null; }

        await LoadHeaderAsync();
        ShowFront();   // همیشه از روی کارت شروع کن

        // انیمیشن ورود کارت
        if (Card is not null)
        {
            Card.Opacity = 0;
            Card.Scale = 0.98;
            _ = Task.WhenAll(
                Card.FadeTo(1, 200, Easing.CubicOut),
                Card.ScaleTo(1, 220, Easing.CubicOut)
            );
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { _shortBeep?.Dispose(); } catch { }
        _shortBeep = null;
    }

    protected override bool OnBackButtonPressed() => true;

    // --- UI state helpers ---

    private async Task LoadHeaderAsync()
    {
        var config = await _db.GetLastConfigAsync();
        if (config == null) return;

        string header = $"بازیکن {config.CurrentPlayerIndex + 1} از {config.Players}";
        PlayerIndexLabelFront.Text = header;
        PlayerIndexLabelBack.Text = header;

        // پاک‌سازی پشت کارت
        WordStack.IsVisible = false;
        SpyStack.IsVisible = false;
        SpyPartnersLabel.IsVisible = false;
        WordValueLabel.Text = string.Empty;
        SpyLabel.Text = string.Empty;
    }

    private void ShowFront()
    {
        _isBackSide = false;
        FrontSide.IsVisible = true;
        FrontSide.InputTransparent = false;

        BackSide.IsVisible = false;
        BackSide.InputTransparent = true;

        // چرخش را ریست کن (اگر از قبل پشت بودیم)
        Card.RotationY = 0;
    }

    private void ShowBack(string? secretWord, bool isSpy, List<int>? allSpyIndices = null, int currentPlayerIndex = -1, bool spiesKnowEachOther = false)
    {
        _isBackSide = true;

        if (isSpy)
        {
            SpyLabel.Text = "جاسوس شدی :)";
            SpyStack.IsVisible = true;
            WordStack.IsVisible = false;

            // نمایش هم‌تیمی‌های جاسوس
            if (spiesKnowEachOther && allSpyIndices != null && allSpyIndices.Count > 1)
            {
                var partners = allSpyIndices
                    .Where(i => i != currentPlayerIndex)
                    .Select(i => $"بازیکن {i + 1}")
                    .ToList();
                if (partners.Any())
                {
                    SpyPartnersLabel.Text = $"هم‌تیمی‌ات: {string.Join("، ", partners)}";
                    SpyPartnersLabel.IsVisible = true;
                }
                else
                {
                    SpyPartnersLabel.IsVisible = false;
                }
            }
            else
            {
                SpyPartnersLabel.IsVisible = false;
            }
        }
        else
        {
            WordValueLabel.Text = secretWord ?? "";
            WordStack.IsVisible = true;
            SpyStack.IsVisible = false;
        }

        BackSide.IsVisible = true;
        BackSide.InputTransparent = false;

        FrontSide.IsVisible = false;
        FrontSide.InputTransparent = true;
    }

    private async Task FlipToBackAsync(string? secretWord, bool isSpy, GameConfig? config = null, int currentIdx = -1)
    {
        if (Card == null)
        {
            ShowBack(secretWord, isSpy, config?.SpyIndices, currentIdx, config?.SpiesKnowEachOther ?? false);
            return;
        }

        await Card.RotateYTo(90, 150u, Easing.CubicIn);
        ShowBack(secretWord, isSpy, config?.SpyIndices, currentIdx, config?.SpiesKnowEachOther ?? false);
        Card.RotationY = -90;
        await Card.RotateYTo(0, 150u, Easing.CubicOut);
    }

    private async Task FlipToFrontAsync()
    {
        if (Card == null) { ShowFront(); return; }

        await Card.RotateYTo(90, 150u, Easing.CubicIn);

        ShowFront();

        Card.RotationY = -90;
        await Card.RotateYTo(0, 150u, Easing.CubicOut);
    }

    // --- Events ---

    private async void OnRevealClicked(object sender, EventArgs e)
    {
        if (_buttonLocked) return;

        var config = await _db.GetLastConfigAsync();
        if (config == null) return;

        int i = config.CurrentPlayerIndex;
        bool isSpy = config.SpyIndices.Contains(i);

        if (!_isBackSide)
        {
            // کلیک اول: نمایش پشت کارت با Flip
            _buttonLocked = true;
            await FlipToBackAsync(config.SecretWord, isSpy, config, i);
            _buttonLocked = false;
        }
        else
        {
            // کلیک دوم: تحویل به نفر بعد / یا شروع تایمر
            _buttonLocked = true;

            try { _shortBeep?.Play(); } catch { }
            try { Vibration.Vibrate(TimeSpan.FromMilliseconds(40)); } catch { }

            config.CurrentPlayerIndex++;
            await _db.UpdateGameConfigAsync(config);

            if (config.CurrentPlayerIndex >= config.Players)
            {
                // پایان مرحلهٔ نمایش نقش‌ها → تایمر
                await Shell.Current.GoToAsync($"{nameof(TimerPage)}");
                return;
            }

            // نفر بعد: برگرد به روی کارت و هدر را به‌روزرسانی کن
            await FlipToFrontAsync();
            await LoadHeaderAsync();

            _buttonLocked = false;
        }
    }

    private async void OnBackToSetup(object sender, EventArgs e)
    {
        try { _shortBeep?.Dispose(); } catch { }

        // حذف کامل دور جاری + آخرین WordHistory (لغو این دور)
        try
        {
            await _db.RollbackLastRoundAsync();
        }
        catch
        {
            // اگر حذف نشد، UX رو خراب نمی‌کنیم. در لاگ می‌تونی ثبت کنی.
        }

        await Shell.Current.GoToAsync($"{nameof(SetupPage)}");
    }
}
