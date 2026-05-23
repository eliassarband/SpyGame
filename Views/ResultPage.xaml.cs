using SpyGame.Data;
using SpyGame.Models;
using SpyGame.Services;

namespace SpyGame.Views;

public partial class ResultPage : ContentPage
{
    private readonly AppDatabase _db;
    private readonly SessionManager _session;

    private GameConfig? _config;
    private int[] _voteCounts = Array.Empty<int>();
    private int _accusedPlayerIndex = -1;

    public ResultPage(AppDatabase db, SessionManager session)
    {
        InitializeComponent();
        _db = db;
        _session = session;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false, IsEnabled = false });

        _config = await _db.GetLastConfigAsync();
        if (_config == null)
        {
            await Shell.Current.GoToAsync(nameof(SetupPage));
            return;
        }

        _voteCounts = new int[_config.Players];
        VotingPhase.IsVisible = true;
        ResultPhase.IsVisible = false;
        AnnounceButton.IsEnabled = false;
        SpyGuessEntry.Text = string.Empty;

        BuildPlayerVoteButtons();
    }

    protected override bool OnBackButtonPressed() => true;

    private void BuildPlayerVoteButtons()
    {
        PlayersList.Children.Clear();

        for (int i = 0; i < (_config?.Players ?? 0); i++)
        {
            int idx = i;

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Margin = new Thickness(0, 2)
            };

            var nameLabel = new Label
            {
                Text = $"بازیکن {i + 1}",
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 16
            };

            var voteBtn = new Button
            {
                Text = "رای",
                BackgroundColor = Color.FromArgb("#546E7A"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44,
                WidthRequest = 80
            };

            var countLabel = new Label
            {
                Text = "0",
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                WidthRequest = 30
            };

            var btnRow = new HorizontalStackLayout
            {
                Spacing = 6,
                HorizontalOptions = LayoutOptions.End
            };
            btnRow.Add(countLabel);
            btnRow.Add(voteBtn);

            voteBtn.Clicked += (_, __) =>
            {
                _voteCounts[idx]++;
                countLabel.Text = _voteCounts[idx].ToString();
                AnnounceButton.IsEnabled = true;
            };

            row.Add(nameLabel);
            Grid.SetColumn(nameLabel, 0);
            row.Add(btnRow);
            Grid.SetColumn(btnRow, 1);

            PlayersList.Children.Add(row);
        }
    }

    private async void OnAnnounceClicked(object sender, EventArgs e)
    {
        if (_config == null) return;

        // پیدا کردن پُررای‌ترین بازیکن
        int maxVotes = _voteCounts.Max();
        _accusedPlayerIndex = Array.IndexOf(_voteCounts, maxVotes);

        bool accusedIsSpy = _config.SpyIndices.Contains(_accusedPlayerIndex);
        bool spyGuessCorrect = false;
        string? spyGuess = SpyGuessEntry.Text?.Trim();

        if (!string.IsNullOrEmpty(spyGuess))
        {
            string normalized = Normalize(spyGuess);
            string normalizedWord = Normalize(_config.SecretWord);
            spyGuessCorrect = normalized == normalizedWord;
        }

        // تعیین برنده
        bool spyWon;
        string verdict;
        Color verdictColor;

        if (!accusedIsSpy)
        {
            spyWon = true;
            verdict = "جاسوس فرار کرد! 🕵️ جاسوس برنده شد!";
            verdictColor = Color.FromArgb("#E53935");
        }
        else if (spyGuessCorrect)
        {
            spyWon = true;
            verdict = "جاسوس لو رفت ولی کلمه رو حدس زد! جاسوس برنده شد!";
            verdictColor = Color.FromArgb("#E53935");
        }
        else
        {
            spyWon = false;
            verdict = "جاسوس لو رفت! بازیکنان بردند!";
            verdictColor = Color.FromArgb("#4CAF50");
        }

        // ثبت در SessionManager
        if (spyWon) _session.RecordSpyWin();
        else _session.RecordPlayerWin();

        // ذخیره در دیتابیس
        await _db.AddGameResultAsync(new GameResult
        {
            Players = _config.Players,
            Spies = _config.Spies,
            CategoryName = _config.CategoryName,
            SecretWord = _config.SecretWord,
            SpyWon = spyWon,
            SpyCaught = accusedIsSpy,
            SpyGuessedCorrectly = spyGuessCorrect
        });

        // آپدیت UI
        VerdictLabel.Text = verdict;
        VerdictLabel.TextColor = verdictColor;

        SecretWordLabel.Text = $"کلمه مخفی: {_config.SecretWord}";

        var spyNames = string.Join("، ", _config.SpyIndices.Select(i => $"بازیکن {i + 1}"));
        SpyNamesLabel.Text = $"جاسوس‌ها: {spyNames}";

        AccusedLabel.Text = $"متهم: بازیکن {_accusedPlayerIndex + 1} (با {maxVotes} رای)";

        if (!string.IsNullOrEmpty(spyGuess))
        {
            GuessResultLabel.IsVisible = true;
            GuessResultLabel.Text = spyGuessCorrect
                ? $"حدس جاسوس «{spyGuess}» درست بود!"
                : $"حدس جاسوس «{spyGuess}» اشتباه بود.";
        }
        else
        {
            GuessResultLabel.IsVisible = false;
        }

        SessionScoreLabel.Text = $"جاسوس: {_session.SpyScore}  |  بازیکنان: {_session.PlayerScore}";
        RoundsLabel.Text = $"تعداد دور: {_session.RoundsPlayed}";

        VotingPhase.IsVisible = false;
        ResultPhase.IsVisible = true;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Trim()
             .Replace('ی', 'ی') // ی
             .Replace('ك', 'ک') // ك → ک
             .Replace("‌", "")       // نیم‌فاصله
             .Replace("​", "");      // zero-width space
        return s.ToLowerInvariant();
    }

    private async void OnNewRound(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SetupPage));
    }

    private async void OnEndGame(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "پایان بازی",
            $"امتیازات ریست می‌شه.\nجاسوس: {_session.SpyScore} | بازیکنان: {_session.PlayerScore}\nمطمئنی؟",
            "بله", "خیر");

        if (confirm)
        {
            _session.Reset();
            await Shell.Current.GoToAsync(nameof(SetupPage));
        }
    }
}
