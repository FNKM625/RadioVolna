namespace RadioVolna.Views;

public partial class SettingsView : ContentView
{
    public event EventHandler? CloseRequested;
    public event EventHandler? AutostartRequested;
    public event EventHandler? AboutRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void OnAutostartClicked(object sender, EventArgs e) => AutostartRequested?.Invoke(this, EventArgs.Empty);
    private void OnAboutClicked(object sender, EventArgs e) => AboutRequested?.Invoke(this, EventArgs.Empty);

    private async void OnLanguageClicked(object sender, EventArgs e)
    {
        await DisplayTempAlert("Język", "Wybór języka wkrótce.");
    }

    private async void OnThemeClicked(object sender, EventArgs e)
    {
        await DisplayTempAlert("Motyw", "Zmiana motywu wkrótce.");
    }

    private async Task DisplayTempAlert(string title, string msg)
    {
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert(title, msg, "OK");
    }
}