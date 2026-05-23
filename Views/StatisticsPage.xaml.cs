using SpyGame.Data;
using SpyGame.Models;

namespace SpyGame.Views;

public partial class StatisticsPage : ContentPage
{
    private readonly AppDatabase _db;

    public StatisticsPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        var results = await _db.GetAllGameResultsAsync();

        int total = results.Count;
        int spyWins = results.Count(r => r.SpyWon);
        int playerWins = total - spyWins;
        double spyWinRate = total > 0 ? (double)spyWins / total * 100 : 0;

        TotalGamesLabel.Text = total.ToString();
        SpyWinsLabel.Text = spyWins.ToString();
        PlayerWinsLabel.Text = playerWins.ToString();
        SpyWinRateLabel.Text = $"{spyWinRate:0}%";

        // محبوب‌ترین دسته
        if (results.Any())
        {
            var favCat = results
                .GroupBy(r => r.CategoryName)
                .OrderByDescending(g => g.Count())
                .First();
            FavCategoryLabel.Text = $"دسته‌بندی: {favCat.Key} ({favCat.Count()} بار)";

            var favWord = results
                .Where(r => !string.IsNullOrEmpty(r.SecretWord))
                .GroupBy(r => r.SecretWord)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (favWord != null && favWord.Count() > 1)
                FavWordLabel.Text = $"پرتکرارترین کلمه: {favWord.Key} ({favWord.Count()} بار)";
            else
                FavWordLabel.Text = string.Empty;
        }
        else
        {
            FavCategoryLabel.Text = "هنوز بازی‌ای ثبت نشده";
            FavWordLabel.Text = string.Empty;
        }

        // آخرین ۵ بازی
        RecentGamesList.Children.Clear();
        foreach (var r in results.Take(5))
        {
            var label = new Label
            {
                Text = $"{r.PlayedOn.ToLocalTime():HH:mm} — {r.CategoryName} — «{r.SecretWord}» — {(r.SpyWon ? "جاسوس برد" : "بازیکنان بردند")}",
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap
            };
            RecentGamesList.Children.Add(label);
        }

        if (!results.Any())
        {
            RecentGamesList.Children.Add(new Label
            {
                Text = "هنوز بازی‌ای ثبت نشده",
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
    }

    private async void OnClearStats(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("پاک کردن آمار", "همه آمار پاک می‌شه. مطمئنی؟", "بله", "خیر");
        if (confirm)
        {
            await _db.ClearGameResultsAsync();
            await LoadStatsAsync();
        }
    }

    private async void OnBack(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SetupPage));
    }
}
