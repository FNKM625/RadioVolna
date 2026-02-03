using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace RadioVolna;

public partial class MainPage : ContentPage
{
    private readonly IAudioService _audioService;
    public ObservableCollection<Station> Stations { get; set; } = new();
    private readonly HttpClient _httpClient = new HttpClient();
    private const string GitHubJsonUrl = "https://raw.githubusercontent.com/FNKM625/RadioVolnaData/refs/heads/main/station.json";

    public MainPage(IAudioService audioService)
    {
        InitializeComponent();
        _audioService = audioService;
        StationsList.ItemsSource = Stations;
        _audioService.StatusChanged += OnStatusChanged;

        LoadStations();
    }

    private void OnStatusChanged(object sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = message;

            if (message.Contains("Błąd") || message.Contains("Brak") || message.Contains("Słaby"))
            {
                StatusLabel.TextColor = Colors.Orange;
            }
            else if (message.Contains("Gra"))
            {
                StatusLabel.TextColor = Colors.LightGreen;
            }
            else
            {
                StatusLabel.TextColor = Colors.White;
            }
        });
    }

    private async void LoadStations()
    {
        try
        {
            var loadedStations = await _httpClient.GetFromJsonAsync<List<Station>>(GitHubJsonUrl);
            if (loadedStations == null) return;

            string favoritesString = Preferences.Get("FavoritesList", "");

            Stations.Clear();
            foreach (var station in loadedStations)
            {
                station.DisplayName = station.Name;
                if (favoritesString.Contains(station.DisplayName))
                {
                    station.IsFavorite = true;
                }
                Stations.Add(station);
            }
            SortStations();
            CheckAndRunAutoStart();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Info", "Błąd ładowania: " + ex.Message, "OK");
        }
    }

    // --- LOGIKA URUCHAMIANIA PRZY STARCIE ---
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

    // --- OBSŁUGA AUTOSTARTU (Z MENU USTAWIEŃ) ---

    private void OnAutostartOptionClicked(object sender, EventArgs e)
    {
        SettingsOverlay.IsVisible = false;

        string currentAutoStartName = Preferences.Get("AutoStartStationName", null);

        if (string.IsNullOrEmpty(currentAutoStartName))
        {
            CurrentAutoStartLabel.Text = "Nie wybrano stacji";
            CurrentAutoStartLabel.TextColor = Colors.Gray;
        }
        else
        {
            CurrentAutoStartLabel.Text = currentAutoStartName;
            CurrentAutoStartLabel.TextColor = Color.FromArgb("#03DAC6");
        }

        var filteredFavorites = Stations
            .Where(s => s.IsFavorite && s.DisplayName != currentAutoStartName)
            .ToList();

        if (filteredFavorites.Count > 0)
        {
            AutoStartList.ItemsSource = filteredFavorites;
        }
        else
        {
            var allStationsFiltered = Stations
                .Where(s => s.DisplayName != currentAutoStartName)
                .ToList();

            AutoStartList.ItemsSource = allStationsFiltered;
        }

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

    private void OnCloseAutoStartClicked(object sender, EventArgs e)
    {
        AutoStartOverlay.IsVisible = false;
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

    private void OnStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            PlayStation(selectedStation);
            StationSelectionOverlay.IsVisible = false;
            StationsList.SelectedItem = null;
        }
    }

    private void OnFavoriteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
        {
            station.IsFavorite = !station.IsFavorite;
            SaveFavorites();
            SortStations();
        }
    }

    private void SaveFavorites()
    {
        var favNames = Stations.Where(s => s.IsFavorite).Select(s => s.DisplayName);
        Preferences.Set("FavoritesList", string.Join("|", favNames));
    }

    private void SortStations()
    {
        var sortedList = Stations.OrderByDescending(s => s.IsFavorite).ThenBy(s => s.DisplayName).ToList();
        Stations.Clear();
        foreach (var s in sortedList) Stations.Add(s);
    }

    // --- OBSŁUGA UI ---
    private void OnSettingsClicked(object sender, EventArgs e) => SettingsOverlay.IsVisible = true;
    private void OnCloseSettingsClicked(object sender, EventArgs e) => SettingsOverlay.IsVisible = false;
    private void OnOpenListClicked(object sender, EventArgs e) => StationSelectionOverlay.IsVisible = true;
    private void OnCloseListClicked(object sender, EventArgs e) => StationSelectionOverlay.IsVisible = false;

    private async void OnSettingsOptionClicked(object sender, EventArgs e)
    {
        if (sender is Button btn) { await btn.FadeTo(0.5, 100); await btn.FadeTo(1.0, 100); }
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

        await NotificationBadge.FadeTo(1, 250, Easing.CubicOut);
        await Task.Delay(2000);
        await NotificationBadge.FadeTo(0, 250, Easing.CubicIn);

        NotificationBadge.IsVisible = false;
        NotificationBadge.InputTransparent = true;
    }
}