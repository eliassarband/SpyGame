using SpyGame.Data;
using SpyGame.Models;

namespace SpyGame.Views;

public partial class SetupPage : ContentPage
{
    private readonly AppDatabase _db;

    // ⬅️ سازندهٔ بدون پارامتر (الزامی برای Shell)
    public SetupPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;

        PlayersStepper.ValueChanged += (_, __) => PlayersLabel.Text = ((int)PlayersStepper.Value).ToString();
        SpiesStepper.ValueChanged += (_, __) => SpiesLabel.Text = ((int)SpiesStepper.Value).ToString();
        MinutesStepper.ValueChanged += (_, __) => MinutesLabel.Text = ((int)MinutesStepper.Value).ToString();
    }

    // سازنده بدون پارامتر (برای Shell) -> DI از ServiceProvider
    //public SetupPage() : this(App.Current.Services.GetRequiredService<AppDatabase>()) { }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var categories = await _db.GetCategoriesAsync();
        CategoryPicker.ItemsSource = categories;

        var general = categories.FirstOrDefault(c => c.Name.Contains("عمومی"));
        CategoryPicker.SelectedItem = general ?? categories.FirstOrDefault();

        var lastConfig = await _db.GetLastConfigAsync();

        if (lastConfig != null)
        {
            PlayersStepper.Value = lastConfig.Players;
            MinutesStepper.Value = lastConfig.Minutes;
            CategoryPicker.SelectedItem = categories.FirstOrDefault(c => c.Id == lastConfig.CategoryId) ?? general;
            SpiesStepper.Value = lastConfig.Spies;
        }
        else
        {
            PlayersStepper.Value = 6;
            MinutesStepper.Value = 3;
            CategoryPicker.SelectedItem = general ?? categories.FirstOrDefault();
            SpiesStepper.Value = 2;
        }
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (_db is null)
        {
            await DisplayAlert("خطا", "دسترسی به دیتابیس برقرار نشد.", "باشه");
            return;
        }

        var selected = CategoryPicker.SelectedItem as Category;
        int players = (int)PlayersStepper.Value;
        int spies = (int)SpiesStepper.Value;
        int minutes = (int)MinutesStepper.Value;

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

        // انتخاب کلمه
        List<WordItem> wordPool;
        if (selected != null && !selected.Name.Contains("عمومی"))
            wordPool = await _db.GetWordsByCategoryAsync(selected.Id);
        else
            wordPool = await _db.GetAllWordsAsync();

        if (wordPool.Count == 0)
        {
            await DisplayAlert("خطا", "برای این دسته هنوز کلمه‌ای وجود ندارد.", "باشه");
            return;
        }

        var rand = new Random();
        var word = wordPool[rand.Next(wordPool.Count)];
        var secret = word.Text;
        await _db.AddWordHistoreAsync(new WordHistory
        {
            WordItemId = word.Id,
        });

        // قرعه‌کشی جاسوس‌ها
        var indices = Enumerable.Range(0, players).ToList();
        var spyIndices = new List<int>();
        for (int i = 0; i < spies; i++)
        {
            int pickAt = rand.Next(indices.Count);
            spyIndices.Add(indices[pickAt]);
            indices.RemoveAt(pickAt);
        }

        var lastConfig = await _db.GetLastConfigAsync();
        if (lastConfig != null)
        {
            lastConfig.Players = players;
            lastConfig.Spies = spies;
            lastConfig.Minutes = minutes;
            lastConfig.CategoryId = selected?.Id;
            lastConfig.CategoryName = selected?.Name ?? "عمومی";
            lastConfig.SecretWord = secret;
            lastConfig.SpyIndices = spyIndices;   // به SpyIndicesJson تبدیل می‌شود
            lastConfig.CurrentPlayerIndex = 0;            // شروع از بازیکن اول
            lastConfig.CreatedOn = DateTime.UtcNow;

            await _db.UpdateGameConfigAsync(lastConfig);
        }
        else
        {
            var newConfig = new GameConfig
            {
                Players = players,
                Spies = spies,
                Minutes = minutes,
                CategoryId = selected?.Id,
                CategoryName = selected?.Name ?? "عمومی",
                SecretWord = secret,
                SpyIndices = spyIndices,
                CurrentPlayerIndex = 0,
                CreatedOn = DateTime.UtcNow
            };
            await _db.AddGameConfigAsync(newConfig);
        }

        await Shell.Current.GoToAsync(nameof(RevealPage));
    }
}
