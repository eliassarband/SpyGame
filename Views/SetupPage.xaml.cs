using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpyGame.Data;
using SpyGame.Models;
using SpyGame.Services;

namespace SpyGame.Views;

public partial class SetupPage : ContentPage
{
    private readonly AppDatabase _db;
    private readonly PremiumManager _premium;

    private record CategoryDisplayItem(int Id, string Name, string DisplayName);

    private int _players = 6;
    private int _spies = 2;
    private int _minutes = 3;

    private static readonly string[] _themes = { "روشن", "تیره", "طبق گوشی" };
    private static readonly string[] _themeIcons = { "☀️", "🌙", "⚙️" };
    private int _themeIndex = 0;

    public SetupPage(AppDatabase db, PremiumManager premium)
    {
        InitializeComponent();
        _db = db;
        _premium = premium;

        CategoryPicker.SelectedIndexChanged += async (_, __) => await UpdateWordCountsAsync();
        DifficultyPicker.SelectedIndexChanged += async (_, __) => await UpdateWordCountsAsync();
    }

    // ---- دکمه‌های + / − ----
    private void OnPlayersMinus(object s, EventArgs e) { if (_players > 3)  { _players--;  PlayersLabel.Text  = _players.ToString(); } }
    private void OnPlayersPlus (object s, EventArgs e) { if (_players < 20) { _players++;  PlayersLabel.Text  = _players.ToString(); } }
    private void OnSpiesMinus  (object s, EventArgs e) { if (_spies > 1)    { _spies--;    SpiesLabel.Text    = _spies.ToString(); } }
    private void OnSpiesPlus   (object s, EventArgs e) { if (_spies < 10)   { _spies++;    SpiesLabel.Text    = _spies.ToString(); } }
    private void OnMinutesMinus(object s, EventArgs e) { if (_minutes > 1)  { _minutes--;  MinutesLabel.Text  = _minutes.ToString(); } }
    private void OnMinutesPlus (object s, EventArgs e) { if (_minutes < 20) { _minutes++;  MinutesLabel.Text  = _minutes.ToString(); } }

    private void OnThemeToggle(object sender, EventArgs e)
    {
        _themeIndex = (_themeIndex + 1) % _themes.Length;
        ApplyAndSaveTheme();
    }

    private void ApplyAndSaveTheme()
    {
        string selected = _themes[_themeIndex];
        if (Application.Current != null)
            Application.Current.UserAppTheme = selected switch
            {
                "تیره" => AppTheme.Dark,
                "طبق گوشی" => AppTheme.Unspecified,
                _ => AppTheme.Light
            };
        Preferences.Set("app_theme", selected);
        ThemeButton.Text = _themeIcons[_themeIndex];
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // انیمیشن لطیف کارت
        if (Card != null)
        {
            Card.Opacity = 0;
            Card.Scale = 0.98;
            _ = Task.WhenAll(
                Card.FadeTo(1, 220, Easing.CubicOut),
                Card.ScaleTo(1, 260, Easing.CubicOut)
            );
        }

        // گزینه‌های سختی (پیش‌فرض: درهم)
        var difficulties = new[] { "درهم", "آسان", "متوسط", "سخت" };
        DifficultyPicker.ItemsSource = difficulties;
        DifficultyPicker.SelectedItem = difficulties[0];

        // بارگذاری دسته‌ها از DB
        var categories = await _db.GetCategoriesAsync();
        bool isPremium = _premium.IsPremium;

        // ساخت آیتم‌های نمایشی — دسته‌های ویژه با 🌟 برچسب دارند
        var displayItems = categories
            .OrderByDescending(c => c.Name == "درهم")
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDisplayItem(
                c.Id, c.Name,
                PremiumManager.PremiumCategoryNames.Contains(c.Name) && !isPremium
                    ? $"{c.Name} 🌟" : c.Name))
            .ToList();

        CategoryPicker.ItemsSource = displayItems;

        var mixedItem = displayItems.FirstOrDefault(di => di.Name == "درهم");
        CategoryPicker.SelectedItem = mixedItem ?? displayItems.FirstOrDefault();

        // نمایش دکمه ارتقاء فقط برای کاربران رایگان
        UpgradeNavButton.IsVisible = !isPremium;

        // بازیابی آخرین تنظیمات (در صورت وجود)
        var lastConfig = await _db.GetLastConfigAsync();
        if (lastConfig != null)
        {
            _players = Math.Clamp(lastConfig.Players, 3, 20);
            _spies   = Math.Clamp(lastConfig.Spies,   1, 10);
            _minutes = Math.Clamp(lastConfig.Minutes,  1, 20);
            CategoryPicker.SelectedItem =
                displayItems.FirstOrDefault(di => di.Id == lastConfig.CategoryId)
                ?? mixedItem
                ?? displayItems.FirstOrDefault();

            var lastDiffFa = ToFa(lastConfig.SelectedDifficulty);
            if (difficulties.Contains(lastDiffFa))
                DifficultyPicker.SelectedItem = lastDiffFa;

            SpiesKnowSwitch.IsToggled = lastConfig.SpiesKnowEachOther;
        }
        else
        {
            _players = 6; _spies = 2; _minutes = 3;
            CategoryPicker.SelectedItem = mixedItem ?? displayItems.FirstOrDefault();
            DifficultyPicker.SelectedItem = "درهم";
            SpiesKnowSwitch.IsToggled = false;
        }

        PlayersLabel.Text  = _players.ToString();
        SpiesLabel.Text    = _spies.ToString();
        MinutesLabel.Text  = _minutes.ToString();

        // بازگردانی آیکن تم
        string savedTheme = Preferences.Get("app_theme", "روشن");
        _themeIndex = Array.IndexOf(_themes, savedTheme);
        if (_themeIndex < 0) _themeIndex = 0;
        ThemeButton.Text = _themeIcons[_themeIndex];

        // شمارنده‌ی کلمات
        await UpdateWordCountsAsync();
    }

    private async Task UpdateWordCountsAsync()
    {
        if (_db is null) return;

        int total = await _db.GetWordCountAsync();

        var selectedItem = CategoryPicker.SelectedItem as CategoryDisplayItem;
        var selectedDifficultyFa = (DifficultyPicker.SelectedItem as string) ?? "درهم";
        DifficultyLevel? diff = FromFa(selectedDifficultyFa);

        List<WordItem> pool;
        if (selectedItem != null && selectedItem.Name != "درهم")
        {
            pool = await _db.GetWordsByCategoryAsync(selectedItem.Id, diff, 500);
            if (pool.Count == 0)
                pool = await _db.GetWordsByCategoryAsync(selectedItem.Id, diff, 0);
        }
        else
        {
            pool = await _db.GetAllWordsAsync(diff, 500);
            if (pool.Count == 0)
                pool = await _db.GetAllWordsAsync(diff, 0);
        }

        WordCountLabel.Text = $"کلمات: کل {total:N0} / قابل انتخاب {pool.Count:N0}";
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (_db is null)
        {
            await DisplayAlert("خطا", "دسترسی به دیتابیس برقرار نشد.", "باشه");
            return;
        }

        var selectedItem = CategoryPicker.SelectedItem as CategoryDisplayItem;
        var selectedDifficultyFa = (DifficultyPicker.SelectedItem as string) ?? "درهم";
        DifficultyLevel? diff = FromFa(selectedDifficultyFa);

        // دروازه ویژه: دسته‌های پریمیوم نیاز به اشتراک دارند
        if (selectedItem != null && PremiumManager.PremiumCategoryNames.Contains(selectedItem.Name) && !_premium.IsPremium)
        {
            bool goUpgrade = await DisplayAlert(
                "دسته ویژه 🌟",
                "این دسته‌بندی مخصوص کاربران ویژه است.",
                "مشاهده بسته ویژه", "بعداً");
            if (goUpgrade)
                await Shell.Current.GoToAsync(nameof(UpgradePage));
            return;
        }

        int players = _players;
        int spies   = _spies;
        int minutes = _minutes;

        // اعتبارسنجی
        if (players < 3)
        {
            await DisplayAlert("خطا", "حداقل ۳ بازیکن لازم است.", "باشه");
            return;
        }
        if (spies < 1)
        {
            await DisplayAlert("خطا", "حداقل ۱ جاسوس لازم است.", "باشه");
            return;
        }
        int maxSpies = players / 2;
        if (spies > maxSpies)
        {
            await DisplayAlert("خطا", $"تعداد جاسوس‌ها نباید بیش از {maxSpies} باشد.", "باشه");
            return;
        }
        if (minutes < 1)
        {
            await DisplayAlert("خطا", "زمان حداقل باید ۱ دقیقه باشد.", "باشه");
            return;
        }

        // انتخاب کلمه با فیلتر Difficulty + جلوگیری از تکرار ۵۰۰ کلمه‌ی آخر
        List<WordItem> wordPool;
        if (selectedItem != null && selectedItem.Name != "درهم")
        {
            wordPool = await _db.GetWordsByCategoryAsync(selectedItem.Id, diff, 500);
            if (wordPool.Count == 0)
                wordPool = await _db.GetWordsByCategoryAsync(selectedItem.Id, diff, 0);
        }
        else
        {
            wordPool = await _db.GetAllWordsAsync(diff, 500);
            if (wordPool.Count == 0)
                wordPool = await _db.GetAllWordsAsync(diff, 0);
        }

        if (wordPool.Count == 0)
        {
            await DisplayAlert("خطا", "برای این ترکیب دسته/درجه سختی، کلمه‌ای پیدا نشد.", "باشه");
            return;
        }

        var rand = new Random();
        var word = wordPool[rand.Next(wordPool.Count)];
        var secret = word.Text;

        // ثبت در تاریخچه کلمات
        await _db.AddWordHistoryAsync(new WordHistory { WordItemId = word.Id });

        // ---------- انتخاب جاسوس‌ها با قوانین جدید ----------
        // قانون 1 (پشت‌سرهم نشدن) و قانون 2 (در N دور اخیر بیش از 3 بار نباشد)
        // تفسیر قانون 2: پنجره = N-1 دور قبلی؛ طوری انتخاب می‌کنیم که با احتساب این دور، سقف 3 رعایت شود.
        int playersCount = players;
        int windowSize = Math.Max(1, playersCount - 1); // N-1
        int cap = 3;

        var lastConfigsForWindow = await _db.GetLastNConfigsAsync(windowSize);
        var lastSpiesHistory = (lastConfigsForWindow ?? new List<GameConfig>())
            .Select(c => (c.SpyIndices ?? new List<int>())
                .Where(i => i >= 0 && i < playersCount)
                .Distinct()
                .ToList())
            .ToList();

        // دور قبلی برای جلوگیری از back-to-back
        var lastRound = await _db.GetLastNConfigsAsync(1);
        var prevRoundSpies = (lastRound?.FirstOrDefault()?.SpyIndices ?? new List<int>())
            .Where(i => i >= 0 && i < playersCount)
            .Distinct()
            .ToHashSet();

        List<int> spyIndices;
        try
        {
            spyIndices = PickSpies_NoBackToBack_And_WindowCap(
                playersCount, spies, lastSpiesHistory, prevRoundSpies, cap, rand);
        }
        catch (InvalidOperationException)
        {
            await DisplayAlert(
                "ناممکن",
                $"با قانون «پشت‌سرهم جاسوس نشدن» و «در {playersCount} دور اخیر بیش از ۳ بار جاسوس نشدن»، " +
                $"به اندازهٔ کافی فرد واجد شرایط برای {spies} جاسوس موجود نیست.\n" +
                "یا تعداد جاسوس‌ها را کاهش دهید یا با بازیکنان بیشتری بازی کنید.",
                "باشه");
            return;
        }
        // ---------------------------------------------------

        // ثبت دور جدید
        var newConfig = new GameConfig
        {
            Players = players,
            Spies = spies,
            Minutes = minutes,
            CategoryId = selectedItem?.Id,
            CategoryName = selectedItem?.Name ?? "درهم",
            SecretWord = secret,
            SpyIndices = spyIndices,
            SelectedDifficulty = diff,
            SpiesKnowEachOther = SpiesKnowSwitch.IsToggled,
            CurrentPlayerIndex = 0,
            CreatedOn = DateTime.UtcNow
        };
        await _db.AddGameConfigAsync(newConfig);

        await Shell.Current.GoToAsync(nameof(RevealPage));
    }

    private async void OnTutorialClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(TutorialPage));

    private async void OnStatisticsClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(StatisticsPage));

    private async void OnCustomWordsClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(CustomWordsPage));

    private async void OnUpgradeClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(UpgradePage));

    // --- نمایش تاریخچه ۵۰۰ کلمهٔ آخر ---
    private async void OnShowHistoryClicked(object sender, EventArgs e)
    {
        try
        {
            if (_db is null)
            {
                await DisplayAlert("خطا", "دسترسی به دیتابیس برقرار نشد.", "باشه");
                return;
            }

            var lastConfigs = await _db.GetLastNConfigsAsync(500);
            var words = (lastConfigs ?? new List<GameConfig>())
                .Where(c => !string.IsNullOrWhiteSpace(c.SecretWord))
                .OrderByDescending(c => c.CreatedOn)
                .Select(c => c.SecretWord!.Trim())
                .ToList();

            if (words.Count == 0)
            {
                await DisplayAlert("تاریخچه", "هنوز کلمه‌ای در تاریخچه نیست.", "باشه");
                return;
            }

            // مودال با اسکرول
            var list = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            for (int i = 0; i < words.Count; i++)
            {
                list.Children.Add(new Label
                {
                    Text = $"{i + 1}. {words[i]}",
                    FontSize = 16,
                    HorizontalTextAlignment = TextAlignment.Start
                });
            }

            var closeBtn = new Button
            {
                Text = "بستن",
                Margin = new Thickness(12, 8),
                HorizontalOptions = LayoutOptions.Center
            };

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            var scroll = new ScrollView { Content = list };
            grid.Add(scroll);
            Grid.SetRow(scroll, 0);

            grid.Add(closeBtn);
            Grid.SetRow(closeBtn, 1);

            var modal = new ContentPage
            {
                Title = "۵۰۰ کلمهٔ آخر",
                FlowDirection = FlowDirection.RightToLeft,
                Content = grid
            };

            closeBtn.Clicked += async (_, __) => await Navigation.PopModalAsync();

            await Navigation.PushModalAsync(new NavigationPage(modal));
        }
        catch
        {
            await DisplayAlert("خطا", "نمایش تاریخچه ممکن نشد.", "باشه");
        }
    }

    // ---- Helpers ----

    private static string ToFa(DifficultyLevel? d) => d switch
    {
        DifficultyLevel.Easy => "آسان",
        DifficultyLevel.Medium => "متوسط",
        DifficultyLevel.Hard => "سخت",
        _ => "درهم"
    };

    private static DifficultyLevel? FromFa(string s) => s switch
    {
        "آسان" => DifficultyLevel.Easy,
        "متوسط" => DifficultyLevel.Medium,
        "سخت" => DifficultyLevel.Hard,
        _ => (DifficultyLevel?)null // درهم
    };

    /// <summary>
    /// انتخاب جاسوس‌ها با دو قید:
    /// 1) هیچ‌کس دو دور پشت‌سرهم (prevRoundSpies) جاسوس نشود.
    /// 2) در پنجرهٔ آخر (lastSpiesHistory) هر نفر بیش از cap بار جاسوس نشده باشد.
    /// </summary>
    private static List<int> PickSpies_NoBackToBack_And_WindowCap(
        int players,
        int spies,
        List<List<int>> lastSpiesHistory,
        HashSet<int> prevRoundSpies,
        int cap,
        Random rand)
    {
        lastSpiesHistory ??= new List<List<int>>();
        prevRoundSpies ??= new HashSet<int>();

        int CountInWindow(int idx)
        {
            int cnt = 0;
            for (int r = 0; r < lastSpiesHistory.Count; r++)
            {
                var spiesInRound = lastSpiesHistory[r] ?? new List<int>();
                if (spiesInRound.Contains(idx)) cnt++;
            }
            return cnt;
        }

        double WeightFor(int countInWindow, bool wasSpyPrevRound) =>
            (wasSpyPrevRound || countInWindow >= cap) ? 0.0 :
            countInWindow switch
            {
                0 => 1.0,
                1 => 0.7,
                2 => 0.4,
                _ => 0.0
            };

        var candidates = Enumerable.Range(0, players).ToList();
        var counts = candidates.ToDictionary(i => i, CountInWindow);
        var weights = candidates.ToDictionary(i => i, i => WeightFor(counts[i], prevRoundSpies.Contains(i)));

        var eligible = new HashSet<int>(weights.Where(kv => kv.Value > 0).Select(kv => kv.Key));
        if (eligible.Count < spies)
            throw new InvalidOperationException("Not enough eligible spies under constraints.");

        // انتخاب وزن‌دار بدون‌جای‌گذاری
        var picks = new List<int>();
        var pool = new HashSet<int>(eligible);

        while (picks.Count < spies && pool.Count > 0)
        {
            double sum = pool.Sum(i => weights[i]);
            if (sum <= 0) break;

            double r = rand.NextDouble() * sum;
            double acc = 0;
            int chosen = -1;
            foreach (var i in pool)
            {
                acc += weights[i];
                if (acc >= r) { chosen = i; break; }
            }
            if (chosen == -1) chosen = pool.Last();

            picks.Add(chosen);
            pool.Remove(chosen);
        }

        while (picks.Count < spies)
        {
            var rest = eligible.Except(picks).ToList();
            if (rest.Count == 0) break;
            var fallback = rest[rand.Next(rest.Count)];
            picks.Add(fallback);
        }

        return picks.OrderBy(x => x).ToList();
    }
}
