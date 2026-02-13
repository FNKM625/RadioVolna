using System.Collections.ObjectModel;
using RadioVolna.Services;
using RadioVolna.Resources;
using System.ComponentModel;

namespace RadioVolna;

public partial class MainPage : ContentPage
{
    private readonly IAudioService _audioService;

    private readonly StationService _stationService = new StationService();
    private readonly StationManager _stationManager = new StationManager();

    private bool _isPlaying = false;

    public ObservableCollection<Station> Stations { get; set; } = new();

    public MainPage(IAudioService audioService)
    {
        InitializeComponent();

        _audioService = audioService;
        StationsList.ItemsSource = Stations;
        _audioService.StatusChanged += OnStatusChanged;
        LocalizationResourceManager.Instance.PropertyChanged += OnLanguageChanged;

        LoadStations();
    }

    private async void LoadStations()
    {
        var loadedStations = await _stationService.GetStationsAsync();

        if (loadedStations.Count == 0)
        {
            string title = LocalizationResourceManager.Instance["TitleError"];
            string msg = LocalizationResourceManager.Instance["MsgStationListError"];
            string ok = LocalizationResourceManager.Instance["BtnOk"];
            await DisplayAlert(title, msg, ok);
            return;
        }

        _stationManager.MergeWithFavorites(loadedStations, Stations);
        #if ANDROID
        if (_audioService is AudioService androidAudioService)
        {
            androidAudioService.UpdateStationsForAuto(Stations.ToList());
        }
        #endif
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

            bool isError = message.Contains("Błąd") || message.Contains("Error") || message.Contains("Ошибка") ||
                           message.Contains("Brak") || message.Contains("Słaby");

            bool isPlaying = message.Contains("Gra") || message.Contains("Playing") || message.Contains("Играет");

            if (isError)
                StatusLabel.TextColor = Colors.Orange;
            else if (isPlaying)
                StatusLabel.TextColor = Colors.LightGreen;
            else
                StatusLabel.TextColor = Colors.DarkGray;
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

                string prefix = LocalizationResourceManager.Instance["MsgAutoStartPrefix"];
                StatusLabel.Text = $"{prefix} {station.DisplayName}";
            }
        }
    }

    private void PlayStation(Station station)
    {
        _audioService.Play(station.Url, station.DisplayName);
        CurrentStationLabel.Text = station.DisplayName;

        StatusLabel.Text = LocalizationResourceManager.Instance["StatusPlayingGeneric"];
        StatusLabel.TextColor = Colors.LightGreen;

        PlayPauseBtn.IsEnabled = true;

        _isPlaying = true;
        UpdatePlayPauseText();
    }

    private void OnPlayPauseClicked(object sender, EventArgs e)
    {
        if (_isPlaying)
        {
            _audioService.Pause();
            _isPlaying = false;

            StatusLabel.Text = LocalizationResourceManager.Instance["NotifPaused"];
            StatusLabel.TextColor = Colors.Orange;
        }
        else
        {
            _audioService.Resume();
            _isPlaying = true;

            StatusLabel.Text = LocalizationResourceManager.Instance["StatusPlayingGeneric"];
            StatusLabel.TextColor = Colors.LightGreen;
        }

        UpdatePlayPauseText();
    }

    // --- UI EVENT HANDLERS ---
    private void OnAutostartOptionClicked(object sender, EventArgs e)
    {
        SettingsOverlayContainer.IsVisible = false;
        string currentAutoStartName = Preferences.Get("AutoStartStationName", null);

        string noStation = LocalizationResourceManager.Instance["MsgNoStationSelected"];

        CurrentAutoStartLabel.Text = string.IsNullOrEmpty(currentAutoStartName) ? noStation : currentAutoStartName;
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

            string prefix = LocalizationResourceManager.Instance["MsgAutoStartPrefix"];
            await ShowNotificationAsync($"{prefix} {selectedStation.DisplayName}");
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
        string displayVersion = AppInfo.Current.VersionString;
        string buildNumber = AppInfo.Current.BuildString;
        string fullVersion = $"{displayVersion} Build({buildNumber})";

        string title = LocalizationResourceManager.Instance["BtnAbout"].Replace("ℹ️ ", "");
        string bodyFormat = LocalizationResourceManager.Instance["MsgAboutBody"];
        string close = LocalizationResourceManager.Instance["BtnClose"];

        await DisplayAlert(title, string.Format(bodyFormat, fullVersion), close);
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
    private void OnLanguageChanged(object sender, PropertyChangedEventArgs e)
    {
        UpdatePlayPauseText();
    }

    private void UpdatePlayPauseText()
    {
        string key = _isPlaying ? "BtnPause" : "BtnResume";
        PlayPauseBtn.Text = LocalizationResourceManager.Instance[key];
    }
}