using System.Collections.ObjectModel;
using System.Net.Http.Json;
using Microsoft.Maui.Storage; // To pozwala zapisywać dane w telefonie

namespace RadioVolna;

public partial class MainPage : ContentPage
{
    // Serwis audio (do grania muzyki)
    private readonly IAudioService _audioService;

    // Lista stacji widoczna na ekranie
    public ObservableCollection<Station> Stations { get; set; } = new();

    // Klient do pobierania danych z internetu
    private readonly HttpClient _httpClient = new HttpClient();

    // Twój link do listy stacji
    private const string GitHubJsonUrl = "https://raw.githubusercontent.com/FNKM625/RadioVolnaData/refs/heads/main/station.json";

    public MainPage(IAudioService audioService)
    {
        InitializeComponent();
        _audioService = audioService;

        // Łączymy listę z widokiem
        StationsList.ItemsSource = Stations;

        // Uruchamiamy pobieranie stacji
        LoadStations();
    }

    private async void LoadStations()
    {
        try
        {
            // 1. Pobierz listę stacji z GitHuba
            var loadedStations = await _httpClient.GetFromJsonAsync<List<Station>>(GitHubJsonUrl);

            if (loadedStations == null) return;

            // 2. Odczytaj zapisane "Ulubione" z pamięci telefonu
            // Jeśli nic nie ma, zwróć pusty tekst ""
            string favoritesString = Preferences.Get("FavoritesList", "");

            Stations.Clear();

            foreach (var station in loadedStations)
            {
                // Przepisujemy nazwę z JSON (label) do wyświetlania
                station.DisplayName = station.Name;

                // 3. Sprawdzamy, czy ta stacja była zapamiętana jako ulubiona
                // (Czy jej nazwa znajduje się w zapisanym tekście)
                if (favoritesString.Contains(station.DisplayName))
                {
                    station.IsFavorite = true;
                }

                Stations.Add(station);
            }

            // 4. Posortuj listę (Ulubione na górę)
            SortStations();
        }
        catch (Exception ex)
        {
            // W razie braku internetu lub błędu
            await DisplayAlert("Info", "Nie udało się pobrać stacji: " + ex.Message, "OK");
        }
    }

    // --- KLIKNIĘCIE W SERDUSZKO ---
    private void OnFavoriteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
        {
            // 1. Zmień stan (z Pustego na Czerwone i odwrotnie)
            station.IsFavorite = !station.IsFavorite;

            // 2. Zapisz zmiany w pamięci telefonu
            SaveFavorites();

            // 3. Przesortuj listę (żeby wskoczyło na górę lub spadło)
            SortStations();
        }
    }

    // --- ZAPISYWANIE DO PAMIĘCI ---
    private void SaveFavorites()
    {
        // Wyciągamy nazwy wszystkich stacji, które mają czerwone serduszko
        var favoriteNames = Stations
            .Where(s => s.IsFavorite)
            .Select(s => s.DisplayName);

        // Łączymy je w jeden długi napis, np.: "Radio ZET|RMF FM|Eska Rock"
        string dataToSave = string.Join("|", favoriteNames);

        // Zapisujemy ten napis w ustawieniach telefonu pod kluczem "FavoritesList"
        Preferences.Set("FavoritesList", dataToSave);
    }

    // --- SORTOWANIE LISTY ---
    private void SortStations()
    {
        // Sortujemy: Najpierw te z serduszkiem (true), potem reszta alfabetycznie
        var sortedList = Stations
            .OrderByDescending(s => s.IsFavorite)
            .ThenBy(s => s.DisplayName)
            .ToList();

        Stations.Clear();
        foreach (var s in sortedList)
        {
            Stations.Add(s);
        }
    }

    // --- POZOSTAŁE PRZYCISKI (Otwieranie listy, Granie, Wyjście) ---

    private void OnOpenListClicked(object sender, EventArgs e)
    {
        StationSelectionOverlay.IsVisible = true;
    }

    private void OnCloseListClicked(object sender, EventArgs e)
    {
        StationSelectionOverlay.IsVisible = false;
    }

    private void OnStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            // Graj wybraną stację
            _audioService.Play(selectedStation.Url, selectedStation.DisplayName);

            // Zaktualizuj napisy na ekranie głównym
            CurrentStationLabel.Text = selectedStation.DisplayName;
            StatusLabel.Text = "Odtwarzanie...";
            StatusLabel.TextColor = Colors.LightGreen;

            // Odblokuj przycisk Pauzy
            PlayPauseBtn.IsEnabled = true;
            PlayPauseBtn.Text = "⏸ PAUZA";

            // Zamknij listę
            StationSelectionOverlay.IsVisible = false;
            StationsList.SelectedItem = null;
        }
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
        System.Diagnostics.Process.GetCurrentProcess().Kill(); // Zamknij aplikację
    }
}