namespace SpyGame.Views;

public partial class TutorialPage : ContentPage
{
    public TutorialPage()
    {
        InitializeComponent();
    }

    private async void OnStartGameClicked(object sender, EventArgs e)
    {
        Preferences.Set("tutorial_shown", true);
        await Shell.Current.GoToAsync(nameof(SetupPage));
    }
}
