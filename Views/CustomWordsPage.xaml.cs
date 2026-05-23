using SpyGame.Data;
using SpyGame.Models;
using SpyGame.Services;

namespace SpyGame.Views;

public partial class CustomWordsPage : ContentPage
{
    private readonly AppDatabase _db;
    private readonly PremiumManager _premium;

    public CustomWordsPage(AppDatabase db, PremiumManager premium)
    {
        InitializeComponent();
        _db = db;
        _premium = premium;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // بارگذاری دسته‌ها (بدون «درهم»)
        var categories = await _db.GetRealCategoriesAsync();
        CategoryPicker.ItemsSource = categories;
        if (categories.Any()) CategoryPicker.SelectedIndex = 0;

        // درجه سختی
        DifficultyPicker.ItemsSource = new[] { "آسان", "متوسط", "سخت" };
        DifficultyPicker.SelectedIndex = 0;

        await LoadWordsAsync();
    }

    private async Task LoadWordsAsync()
    {
        var words = await _db.GetCustomWordsAsync();
        bool isPremium = _premium.IsUnlocked(PremiumFeature.Premium);

        if (isPremium)
        {
            QuotaLabel.Text = "✅ کلمات سفارشی نامحدود (ویژه)";
            UpgradeHintButton.IsVisible = false;
        }
        else
        {
            int count = words.Count;
            QuotaLabel.Text = $"{count} از {PremiumManager.FreeCustomWordLimit} کلمه رایگان استفاده شده";
            UpgradeHintButton.IsVisible = count >= PremiumManager.FreeCustomWordLimit;
        }

        WordsList.Children.Clear();
        EmptyLabel.IsVisible = !words.Any();

        foreach (var word in words)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Margin = new Thickness(0, 2)
            };

            var label = new Label
            {
                Text = $"{word.Text}  ({DiffToFa(word.Difficulty)})",
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 14
            };

            var deleteBtn = new Button
            {
                Text = "حذف",
                BackgroundColor = Color.FromArgb("#E53935"),
                TextColor = Colors.White,
                CornerRadius = 6,
                HeightRequest = 36,
                Padding = new Thickness(10, 0)
            };

            int wordId = word.Id;
            deleteBtn.Clicked += async (_, __) =>
            {
                bool confirm = await DisplayAlert("حذف کلمه", $"«{word.Text}» حذف بشه؟", "بله", "خیر");
                if (confirm)
                {
                    await _db.DeleteWordByIdAsync(wordId);
                    await LoadWordsAsync();
                }
            };

            row.Add(label);
            Grid.SetColumn(label, 0);
            row.Add(deleteBtn);
            Grid.SetColumn(deleteBtn, 1);

            WordsList.Children.Add(row);
        }
    }

    private async void OnAddWordClicked(object sender, EventArgs e)
    {
        string text = WordEntry.Text?.Trim() ?? string.Empty;

        if (!_premium.IsUnlocked(PremiumFeature.Premium))
        {
            var count = await _db.GetCustomWordsCountAsync();
            if (count >= PremiumManager.FreeCustomWordLimit)
            {
                bool goUpgrade = await DisplayAlert(
                    "محدودیت رایگان",
                    $"کاربران رایگان تا {PremiumManager.FreeCustomWordLimit} کلمه سفارشی می‌توانند اضافه کنند.",
                    "مشاهده بسته ویژه", "بعداً");
                if (goUpgrade)
                    await Shell.Current.GoToAsync(nameof(UpgradePage));
                return;
            }
        }

        if (string.IsNullOrEmpty(text))
        {
            await DisplayAlert("خطا", "کلمه را وارد کنید.", "باشه");
            return;
        }
        if (text.Length > 30)
        {
            await DisplayAlert("خطا", "کلمه نباید بیشتر از ۳۰ کاراکتر باشه.", "باشه");
            return;
        }
        if (CategoryPicker.SelectedItem is not Category selectedCategory)
        {
            await DisplayAlert("خطا", "دسته‌بندی را انتخاب کنید.", "باشه");
            return;
        }

        DifficultyLevel diff = (DifficultyPicker.SelectedItem as string) switch
        {
            "آسان" => DifficultyLevel.Easy,
            "سخت" => DifficultyLevel.Hard,
            _ => DifficultyLevel.Medium
        };

        var newWord = new WordItem
        {
            Text = text,
            CategoryId = selectedCategory.Id,
            Difficulty = diff,
            IsCustom = true
        };

        await _db.AddCustomWordAsync(newWord);
        WordEntry.Text = string.Empty;
        await LoadWordsAsync();

        await DisplayAlert("موفق", $"کلمه «{text}» اضافه شد.", "باشه");
    }

    private static string DiffToFa(DifficultyLevel d) => d switch
    {
        DifficultyLevel.Easy => "آسان",
        DifficultyLevel.Hard => "سخت",
        _ => "متوسط"
    };

    private async void OnUpgradeClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(UpgradePage));

    private async void OnBack(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SetupPage));
    }
}
