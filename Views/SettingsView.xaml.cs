using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using RadioVolna.Resources;
#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
#endif

namespace RadioVolna.Views;

public partial class SettingsView : ContentView
{
    public event EventHandler? CloseRequested;
    public event EventHandler? AutostartRequested;
    public event EventHandler? AboutRequested;

    public SettingsView()
    {
        InitializeComponent();
        this.Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        CheckBatteryStatus();
    }

    public void CheckBatteryStatus()
    {
#if ANDROID
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                var context = Platform.CurrentActivity?.ApplicationContext;
                var packageName = context?.PackageName;
                var pm = (PowerManager?)context?.GetSystemService(Context.PowerService);

                if (pm != null && packageName != null)
                {
                    bool isIgnoring = pm.IsIgnoringBatteryOptimizations(packageName);

                    if (isIgnoring)
                    {
                        BatteryBtn.Text = LocalizationResourceManager.Instance["BatteryStatusGood"];
                        BatteryBtn.TextColor = Colors.LightGreen;
                        BatteryBtn.BorderColor = Colors.LightGreen;
                        BatteryBtn.IsEnabled = false;
                    }
                    else
                    {
                        BatteryBtn.Text = LocalizationResourceManager.Instance["BatteryStatusBad"];
                        BatteryBtn.TextColor = Colors.Orange;
                        BatteryBtn.BorderColor = Colors.Orange;
                        BatteryBtn.IsEnabled = true;
                    }
                    return;
                }
            }
        }
#endif
        BatteryBtn.IsVisible = false;
    }

    private void OnBatterySettingsClicked(object sender, EventArgs e)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
#pragma warning disable CA1416

            try
            {
                var packageName = Platform.CurrentActivity?.ApplicationContext?.PackageName;
                var intent = new Intent();

                intent.SetAction(Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
                intent.SetFlags(ActivityFlags.NewTask);
                Platform.CurrentActivity?.StartActivity(intent);
            }
            catch
            {
                try
                {
                    var intent = new Intent(Settings.ActionIgnoreBatteryOptimizationSettings);
                    intent.SetFlags(ActivityFlags.NewTask);
                    Platform.CurrentActivity?.StartActivity(intent);
                }
                catch { }
            }

#pragma warning restore CA1416
        }
#endif
    }

    private void OnCloseClicked(object sender, EventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnAutostartClicked(object sender, EventArgs e) =>
        AutostartRequested?.Invoke(this, EventArgs.Empty);

    private void OnAboutClicked(object sender, EventArgs e) =>
        AboutRequested?.Invoke(this, EventArgs.Empty);

    private async void OnLanguageClicked(object sender, EventArgs e)
    {
        if (Application.Current?.MainPage == null)
            return;

        string title = LocalizationResourceManager.Instance["DialogLangTitle"];
        string cancel = LocalizationResourceManager.Instance["DialogCancel"];

        string action = await Application.Current.MainPage.DisplayActionSheet(
            title,
            cancel,
            null,
            "Polski",
            "English",
            "Русский"
        );

        string? code = action switch
        {
            "Polski" => "pl",
            "English" => "en",
            "Русский" => "ru",
            _ => null,
        };

        if (code == null)
            return;

        CultureInfo newCulture = new CultureInfo(code);
        LocalizationResourceManager.Instance.SetCulture(newCulture);
        Preferences.Set("Language", code);
    }

    private void OnThemeClicked(object sender, EventArgs e)
    {
        if (Application.Current == null)
            return;

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
