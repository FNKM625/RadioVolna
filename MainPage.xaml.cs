using System.Collections.ObjectModel;
using RadioVolna.Services;

namespace RadioVolna;

public partial class MainPage : ContentPage
{
    private readonly IAudioService _audioService;

    private readonly StationService _stationService = new StationService();
    private readonly StationManager _stationManager = new StationManager();

    public ObservableCollection<Station> Stations { get; set; } = new();

    public MainPage(IAudioService audioService)
    {
        InitializeComponent();

        _audioService = audioService;
        StationsList.ItemsSource = Stations;
        _audioService.StatusChanged += OnStatusChanged;

        LoadStations();
    }

    private async void LoadStations()
    {
        var loadedStations = await _stationService.GetStationsAsync();

        if (loadedStations.Count == 0)
        {
            await DisplayAlert("Błąd", "Nie udało się pobrać listy stacji.", "OK");
            return;
        }

        _stationManager.MergeWithFavorites(loadedStations, Stations);

        CheckAndRunAutoStart();
    }

    private void OnFavoriteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
        {
            _stationManager.ToggleFavorite(station, Stations);
        }
    }

    // --- PONIŻEJ TYLKO OBSŁUGA UI I ODTWARZANIA ---

    private void OnStatusChanged(object sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = message;
            if (message.Contains("Błąd") || message.Contains("Brak") || message.Contains("Słaby"))
                StatusLabel.TextColor = Colors.Orange;
            else if (message.Contains("Gra"))
                StatusLabel.TextColor = Colors.LightGreen;
            else
                StatusLabel.TextColor = Colors.White;
        });
    }

    private async void CheckAndRunAutoStart()
    {
        string autoStartName = Preferences.Get("AutoStartStationName", null);
        if (!string.IsNullOrEmpty(autoStartName))
        {
            var station = Stations.FirstOrDefault(s => s.DisplayName == autoStartName);
            if (station != null)
            {
                await Task.Delay(500);
                PlayStation(station);
                StatusLabel.Text = $"Autostart: {station.DisplayName}";
            }
        }
    }

    private void PlayStation(Station station)
    {
        _audioService.Play(station.Url, station.DisplayName);
        CurrentStationLabel.Text = station.DisplayName;
        StatusLabel.Text = "Odtwarzanie...";
        StatusLabel.TextColor = Colors.LightGreen;
        PlayPauseBtn.IsEnabled = true;
        PlayPauseBtn.Text = "⏸ PAUZA";
    }

    private void OnPlayPauseClicked(object sender, EventArgs e)
    {
        if (PlayPauseBtn.Text.Contains("PAUZA"))
        {
            _audioService.Pause();
            PlayPauseBtn.Text = "▶ WZNÓW";
            StatusLabel.Text = "Wstrzymano";
            StatusLabel.TextColor = Colors.Orange;
        }
        else
        {
            _audioService.Resume();
            PlayPauseBtn.Text = "⏸ PAUZA";
            StatusLabel.Text = "Odtwarzanie...";
            StatusLabel.TextColor = Colors.LightGreen;
        }
    }

    // --- UI EVENT HANDLERS ---
    private void OnAutostartOptionClicked(object sender, EventArgs e)
    {
        SettingsOverlayContainer.IsVisible = false;
        string currentAutoStartName = Preferences.Get("AutoStartStationName", null);

        CurrentAutoStartLabel.Text = string.IsNullOrEmpty(currentAutoStartName) ? "Nie wybrano stacji" : currentAutoStartName;
        CurrentAutoStartLabel.TextColor = string.IsNullOrEmpty(currentAutoStartName) ? Colors.Gray : Color.FromArgb("#03DAC6");

        var filteredList = Stations.Where(s => s.DisplayName != currentAutoStartName).OrderByDescending(s => s.IsFavorite).ToList();
        AutoStartList.ItemsSource = filteredList;

        AutoStartOverlay.IsVisible = true;
    }

    private async void OnAutoStartStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            Preferences.Set("AutoStartStationName", selectedStation.DisplayName);
            AutoStartList.SelectedItem = null;
            AutoStartOverlay.IsVisible = false;
            await ShowNotificationAsync($"Autostart: {selectedStation.DisplayName}");
        }
    }

    private void OnStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            PlayStation(selectedStation);
            StationSelectionOverlay.IsVisible = false;
            StationsList.SelectedItem = null;
        }
    }

    private void OnCloseAutoStartClicked(object sender, EventArgs e) => AutoStartOverlay.IsVisible = false;
    private void OnOpenListClicked(object sender, EventArgs e) => StationSelectionOverlay.IsVisible = true;
    private void OnCloseListClicked(object sender, EventArgs e) => StationSelectionOverlay.IsVisible = false;
    private void OnSettingsClicked(object sender, EventArgs e) => SettingsOverlayContainer.IsVisible = true;
    private void OnCloseSettingsClicked(object sender, EventArgs e) => SettingsOverlayContainer.IsVisible = false;

    private async void OnAboutClicked(object sender, EventArgs e)
    {
        string v = AppInfo.Current.VersionString;
        await DisplayAlert("O aplikacji", $"Radio Volna\nFNKMG\nv{v}\n© 2026", "Zamknij");
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        _audioService.Stop();
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }

    private async Task ShowNotificationAsync(string message)
    {
        NotificationLabel.Text = message;
        NotificationBadge.IsVisible = true;
        NotificationBadge.InputTransparent = false;
        await NotificationBadge.FadeTo(1, 250);
        await Task.Delay(2000);
        await NotificationBadge.FadeTo(0, 250);
        NotificationBadge.IsVisible = false;
        NotificationBadge.InputTransparent = true;
    }
}