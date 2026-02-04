using Microsoft.Maui.Controls;
using RadioVolna.Resources;
using System.Globalization;

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
        string title = LocalizationResourceManager.Instance["DialogLangTitle"];
        string cancel = LocalizationResourceManager.Instance["DialogCancel"];

        string action = await Application.Current.MainPage.DisplayActionSheet(
            title, cancel, null, "Polski", "English", "Русский");

        string code = action switch
        {
            "Polski" => "pl",
            "English" => "en",
            "Русский" => "ru",
            _ => null
        };

        if (code == null) return;

        CultureInfo newCulture = new CultureInfo(code);
        LocalizationResourceManager.Instance.SetCulture(newCulture);
        Preferences.Set("Language", code);
    }

    private void OnThemeClicked(object sender, EventArgs e)
    {
        var currentTheme = Application.Current.UserAppTheme;

        if (currentTheme == AppTheme.Unspecified)
            currentTheme = Application.Current.RequestedTheme;

        var newTheme = currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;

        Application.Current.UserAppTheme = newTheme;

        Preferences.Set("AppTheme", newTheme.ToString());
    }

    private async Task DisplayTempAlert(string title, string msg)
    {
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert(title, msg, "OK");
    }
}