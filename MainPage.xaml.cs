using System.Collections.ObjectModel;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;

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

        // --- NOWOŚĆ: Nasłuchujemy zmian statusu z AudioService ---
        _audioService.StatusChanged += (s, message) =>
        {
            // Musimy to robić w "MainThread", bo zmieniamy wygląd aplikacji
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Jeśli wiadomość zaczyna się od "Gra:", to znaczy że połączono
                if (message.StartsWith("Gra:"))
                {
                    StatusLabel.Text = "Odtwarzanie...";
                    StatusLabel.TextColor = Colors.LightGreen;
                }
                else
                {
                    // W przeciwnym razie wyświetlamy status (np. "Łączenie...", "Błąd...")
                    StatusLabel.Text = message;
                    StatusLabel.TextColor = Colors.Orange;
                }
            });
        };
        // ---------------------------------------------------------

        LoadStations();
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
                // Tu też zmieniamy na status łączenia
                StatusLabel.Text = "Autostart (Łączenie...)";
            }
        }
    }

    private void OnAutostartOptionClicked(object sender, EventArgs e)
    {
        SettingsOverlay.IsVisible = false;
        var favorites = Stations.Where(s => s.IsFavorite).ToList();

        if (favorites.Count > 0)
        {
            AutoStartList.ItemsSource = favorites;
            AutoStartHeaderLabel.Text = "Wybierz z ulubionych";
        }
        else
        {
            AutoStartList.ItemsSource = Stations;
            AutoStartHeaderLabel.Text = "Wybierz stację (Brak ulubionych)";
        }
        AutoStartOverlay.IsVisible = true;
    }

    private async void OnAutoStartStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            Preferences.Set("AutoStartStationName", selectedStation.DisplayName);
            await DisplayAlert("Sukces", $"Ustawiono autostart na:\n{selectedStation.DisplayName}", "OK");
            AutoStartOverlay.IsVisible = false;
            AutoStartList.SelectedItem = null;
        }
    }

    private void OnCloseAutoStartClicked(object sender, EventArgs e) => AutoStartOverlay.IsVisible = false;

    // --- POPRAWIONA METODA PLAY ---
    private void PlayStation(Station station)
    {
        _audioService.Play(station.Url, station.DisplayName);
        CurrentStationLabel.Text = station.DisplayName;

        // ZMIANA: Ustawiamy "Łączenie..." i kolor pomarańczowy
        StatusLabel.Text = "Łączenie...";
        StatusLabel.TextColor = Colors.Orange;

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
            // Przy wznowieniu też może chwilę łączyć
            StatusLabel.Text = "Wznawianie...";
            StatusLabel.TextColor = Colors.Orange;
        }
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        _audioService.Stop();
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }
}