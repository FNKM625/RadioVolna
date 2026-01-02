#pragma warning disable CS0168

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RadioVolna;

public partial class MainPage : ContentPage
{
    private const string JsonUrl = "https://raw.githubusercontent.com/FNKM625/RadioVolnaData/refs/heads/main/station.json";
    private readonly IAudioService _audioService;
    private readonly HttpClient _httpClient = new HttpClient();

    private bool _isPlaying = false;

    public ObservableCollection<Station> Stations { get; set; } = new();

    public MainPage(IAudioService audioService)
    {
        InitializeComponent();
        _audioService = audioService;
        StationsList.ItemsSource = Stations;

        _audioService.StatusChanged += (s, msg) =>
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg);

        _audioService.IsPlayingChanged += (s, isPlaying) =>
        {
            _isPlaying = isPlaying;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PlayPauseBtn.IsEnabled = true;

                if (isPlaying)
                {
                    PlayPauseBtn.Text = "⏸ PAUZA";
                    PlayPauseBtn.BackgroundColor = Color.FromArgb("#DA7903");
                    PlayPauseBtn.TextColor = Colors.Black;
                }
                else
                {
                    PlayPauseBtn.Text = "▶ GRAJ";
                    PlayPauseBtn.BackgroundColor = Color.FromArgb("#6200EE");
                    PlayPauseBtn.TextColor = Colors.White;
                }
            });
        };

        LoadStations();
    }

    private void OnPlayPauseClicked(object sender, EventArgs e)
    {
        if (_isPlaying)
        {
            _audioService.Pause();
        }
        else
        {
            _audioService.Resume();
        }
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        _audioService.Stop();
        Application.Current?.Quit();
    }

    private async void LoadStations()
    {
        StatusLabel.Text = "Aktualizacja listy...";
        try
        {
            var response = await _httpClient.GetStringAsync(JsonUrl);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var stations = JsonSerializer.Deserialize<List<Station>>(response, options);

            if (stations != null)
            {
                Stations.Clear();
                foreach (var station in stations)
                {
                    station.DisplayName = RemoveEmojis(station.Name).Trim();
                    Stations.Add(station);
                }
                StatusLabel.Text = "Wybierz stację";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Błąd listy stacji";
        }
    }

    private string RemoveEmojis(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return Regex.Replace(text, @"\p{Cs}|\p{So}|\p{Cn}", "");
    }

    private void OnOpenListClicked(object sender, EventArgs e) => StationSelectionOverlay.IsVisible = true;
    private void OnCloseListClicked(object sender, EventArgs e) => StationSelectionOverlay.IsVisible = false;

    private void OnStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            _audioService.Play(selectedStation.Url, selectedStation.DisplayName);
            CurrentStationLabel.Text = selectedStation.DisplayName;
            StationSelectionOverlay.IsVisible = false;
            StationsList.SelectedItem = null;
        }
    }
}