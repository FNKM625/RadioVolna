using System.Collections.ObjectModel;
using System.ComponentModel;
using RadioVolna.Resources;
using RadioVolna.Services;

namespace RadioVolna;

public partial class MainPage : ContentPage
{
    private readonly IAudioService _audioService;
    private readonly StationService _stationService = new StationService();
    private readonly StationManager _stationManager = new StationManager();
    private bool _isPlaying = false;
    public ObservableCollection<Station> Stations { get; set; } = new();
    private Station? _stationBeforePreview;

    public MainPage(IAudioService audioService)
    {
        InitializeComponent();

        _audioService = audioService;
        StationSelectionOverlay.ItemsSource = Stations;
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

        var customStations = _stationManager.LoadCustomStations();
        foreach (var customStation in customStations)
        {
            if (!Stations.Any(s => s.Url == customStation.Url))
            {
                Stations.Add(customStation);
            }
        }

#if ANDROID
        if (_audioService is AudioService androidAudioService)
        {
            androidAudioService.UpdateStationsForAuto(Stations.ToList());
        }
#endif
        CheckAndRunAutoStart();
    }

    // --- PONIŻEJ TYLKO OBSŁUGA UI I ODTWARZANIA ---

    private void OnStatusChanged(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = message;

            bool isError =
                message.Contains("Błąd")
                || message.Contains("Error")
                || message.Contains("Ошибка")
                || message.Contains("Brak")
                || message.Contains("Słaby");

            bool isPlaying =
                message.Contains("Gra")
                || message.Contains("Playing")
                || message.Contains("Играет");

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
        string? autoStartName = Preferences.Get("AutoStartStationName", (string?)null);
        if (!string.IsNullOrEmpty(autoStartName))
        {
            var station = Stations.FirstOrDefault(s => s.DisplayName == autoStartName);
            if (station != null)
            {
                _isPlaying = true;
                UpdatePlayPauseText();

                await Task.Delay(500);

                if (_isPlaying)
                {
                    PlayStation(station);
                    string prefix = LocalizationResourceManager.Instance["MsgAutoStartPrefix"];
                    StatusLabel.Text = $"{prefix} {station.DisplayName}";
                }
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
            _audioService.Stop();
            _isPlaying = false;

            StatusLabel.Text = LocalizationResourceManager.Instance["NotifPaused"];
            StatusLabel.TextColor = Colors.Orange;
        }
        else
        {
            _isPlaying = true;

            var currentStationName = CurrentStationLabel.Text;
            var stationToPlay = Stations.FirstOrDefault(s => s.DisplayName == currentStationName);

            if (stationToPlay != null)
            {
                _audioService.Play(stationToPlay.Url, stationToPlay.DisplayName);
            }
            else
            {
                _audioService.Resume();
            }

            StatusLabel.Text = LocalizationResourceManager.Instance["StatusPlayingGeneric"];
            StatusLabel.TextColor = Colors.LightGreen;
        }

        UpdatePlayPauseText();
    }

    // --- UI EVENT HANDLERS ---
    private void OnAutostartOptionClicked(object sender, EventArgs e)
    {
        SettingsOverlayContainer.IsVisible = false;

        string? currentAutoStartName = Preferences.Get("AutoStartStationName", (string?)null);

        string noStation = LocalizationResourceManager.Instance["MsgNoStationSelected"];
        CurrentAutoStartLabel.Text = string.IsNullOrEmpty(currentAutoStartName)
            ? noStation
            : currentAutoStartName;
        CurrentAutoStartLabel.TextColor = string.IsNullOrEmpty(currentAutoStartName)
            ? Colors.Gray
            : Color.FromArgb("#03DAC6");

        var availableStations = Stations.Where(s => s.DisplayName != currentAutoStartName).ToList();

        var availableFavorites = availableStations.Where(s => s.IsFavorite).ToList();

        if (availableFavorites.Count > 0)
        {
            AutoStartList.ItemsSource = availableFavorites.OrderBy(s => s.DisplayName).ToList();
        }
        else
        {
            AutoStartList.ItemsSource = availableStations.OrderBy(s => s.DisplayName).ToList();
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

            string prefix = LocalizationResourceManager.Instance["MsgAutoStartPrefix"];
            await ShowNotificationAsync($"{prefix} {selectedStation.DisplayName}");
        }
    }

    private void OnCloseAutoStartClicked(object sender, EventArgs e) =>
        AutoStartOverlay.IsVisible = false;

    private void OnOpenListClicked(object sender, EventArgs e) =>
        StationSelectionOverlay.IsVisible = true;

    private void OnSettingsClicked(object sender, EventArgs e)
    {
        SettingsOverlayContainer.IsVisible = true;

        if (SettingsOverlayContainer.Children.FirstOrDefault() is Views.SettingsView settingsView)
        {
            settingsView.CheckBatteryStatus();
        }
    }

    private void OnCloseSettingsClicked(object sender, EventArgs e) =>
        SettingsOverlayContainer.IsVisible = false;

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

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdatePlayPauseText();
    }

    private void UpdatePlayPauseText()
    {
        string key = _isPlaying ? "BtnPause" : "BtnResume";
        PlayPauseBtn.Text = LocalizationResourceManager.Instance[key];
    }

    // --- OBSŁUGA STATION SELECTION VIEW ---

    private void OnStationSelectionView_StationSelected(object sender, Station selectedStation)
    {
        PlayStation(selectedStation);
        StationSelectionOverlay.IsVisible = false;
        StationSelectionOverlay.ClearSelection();
    }

    private void OnStationSelectionView_FavoriteClicked(object sender, Station station)
    {
        _stationManager.ToggleFavorite(station, Stations);
    }

    private void OnStationSelectionView_CloseRequested(object sender, EventArgs e)
    {
        StationSelectionOverlay.IsVisible = false;
    }

    private async void OnStationSelectionView_DeleteClicked(object sender, Station station)
    {
        string title = LocalizationResourceManager.Instance["DialogDeleteTitle"];
        string msgFormat = LocalizationResourceManager.Instance["DialogDeleteMsg"];
        string yes = LocalizationResourceManager.Instance["DialogYes"];
        string no = LocalizationResourceManager.Instance["DialogNo"];

        bool answer = await DisplayAlert(
            title,
            string.Format(msgFormat, station.DisplayName),
            yes,
            no
        );

        if (answer)
        {
            _stationManager.DeleteCustomStation(station);
            Stations.Remove(station);

            if (CurrentStationLabel.Text == station.DisplayName)
            {
                _audioService.Stop();
                CurrentStationLabel.Text = LocalizationResourceManager.Instance["MsgSelectStation"];
                StatusLabel.Text = LocalizationResourceManager.Instance["StatusReady"];
            }
        }
    }

    // --- OBSŁUGA DODAWANIA STACJI ---

    private void OnOpenAddStationMenuClicked(object sender, EventArgs e)
    {
        AddStationMenuOverlay.IsOpen = true;
    }

    private void OnAddCustomStationClicked(object sender, EventArgs e)
    {
        AddCustomStationOverlay.ClearForm();
        AddCustomStationOverlay.IsOpen = true;
    }

    private void OnSearchStationClicked(object sender, EventArgs e)
    {
        SearchStationFilterOverlay.Reset();
        SearchStationFilterOverlay.IsOpen = true;
    }

    // --- OBSŁUGA DODAWANIA WŁASNEJ STACJI ---
    private void OnCustomStationCancelClicked(object sender, EventArgs e)
    {
        // Po prostu zamykamy okienko
        AddCustomStationOverlay.IsOpen = false;
    }

    private async void OnCustomStationPreviewClicked(object sender, EventArgs e)
    {
        string url = AddCustomStationOverlay.StationUrl;
        string name = AddCustomStationOverlay.StationName;

        if (string.IsNullOrWhiteSpace(url))
        {
            await DisplayAlert(
                LocalizationResourceManager.Instance["DialogErrorTitle"],
                LocalizationResourceManager.Instance["ErrorProvideLink"],
                "OK"
            );
            return;
        }

        _audioService.Play(url, string.IsNullOrWhiteSpace(name) ? "Test streamu..." : name);
        StatusLabel.Text = LocalizationResourceManager.Instance["StatusTestingLink"];
        StatusLabel.TextColor = Colors.LightBlue;
    }

    private async void OnCustomStationSaveClicked(object sender, EventArgs e)
    {
        string name = AddCustomStationOverlay.StationName;
        string url = AddCustomStationOverlay.StationUrl;
        string emoji = AddCustomStationOverlay.StationEmoji;

        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert(
                LocalizationResourceManager.Instance["DialogErrorTitle"],
                LocalizationResourceManager.Instance["ErrorStationNameReq"],
                "OK"
            );
            return;
        }

        bool isValidUrl =
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        if (!isValidUrl)
        {
            await DisplayAlert(
                LocalizationResourceManager.Instance["DialogErrorTitle"],
                LocalizationResourceManager.Instance["ErrorInvalidLink"],
                "OK"
            );
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(emoji) ? name : $"{emoji} {name}";
        var newStation = new Station
        {
            Name = displayName,
            DisplayName = displayName,
            Url = url,
            IsFavorite = false,
        };

        Stations.Add(newStation);
        _stationManager.SaveCustomStation(newStation);

        string successTitle = LocalizationResourceManager.Instance["DialogSuccessTitle"];
        string successMsg = string.Format(
            LocalizationResourceManager.Instance["MsgStationAdded"],
            displayName
        );
        await DisplayAlert(successTitle, successMsg, "OK");

        AddCustomStationOverlay.IsOpen = false;
    }

    // --- OBSŁUGA WYSZUKIWANIA W BAZIE (RADIO BROWSER) ---

    private void OnCancelApiSearchClicked(object sender, EventArgs e)
    {
        SearchStationFilterOverlay.IsOpen = false;
    }

    private async void OnExecuteApiSearchClicked(object sender, EventArgs e)
    {
        string name = SearchStationFilterOverlay.SearchName;
        string country = SearchStationFilterOverlay.SearchCountry;
        string tags = SearchStationFilterOverlay.SearchTags;

        if (
            string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(country)
            && string.IsNullOrWhiteSpace(tags)
        )
        {
            await DisplayAlert(
                LocalizationResourceManager.Instance["DialogErrorTitle"],
                LocalizationResourceManager.Instance["ErrorFillOneField"],
                "OK"
            );
            return;
        }

        SearchStationFilterOverlay.IsOpen = false;
        StatusLabel.Text = LocalizationResourceManager.Instance["StatusSearchingDb"];
        StatusLabel.TextColor = Colors.LightBlue;

        var results = await _stationService.SearchRadioBrowserAsync(name, country, tags);

        var filteredResults = results
            .Where(apiStation => !Stations.Any(myStation => myStation.Url == apiStation.Url))
            .ToList();
        StatusLabel.Text = LocalizationResourceManager.Instance["StatusReady"];

        if (filteredResults.Count == 0)
        {
            await DisplayAlert(
                LocalizationResourceManager.Instance["TitleNoNewStations"],
                LocalizationResourceManager.Instance["MsgAllStationsOnList"],
                "OK"
            );
            return;
        }

        SearchStationResultsOverlay.SetItems(filteredResults);
        SearchStationResultsOverlay.IsOpen = true;
    }

    private void OnSearchResultPreviewClicked(object sender, Station selectedStation)
    {
        if (selectedStation.IsPreviewing)
        {
            StopPreviewAndResumePrevious(selectedStation);
        }
        else
        {
            if (SearchStationResultsOverlay.CurrentStations != null)
            {
                foreach (var s in SearchStationResultsOverlay.CurrentStations)
                    s.IsPreviewing = false;
            }

            if (_stationBeforePreview == null)
            {
                _stationBeforePreview = Stations.FirstOrDefault(s =>
                    s.DisplayName == CurrentStationLabel.Text
                );
            }

            selectedStation.IsPreviewing = true;
            _audioService.Play(selectedStation.Url, $"Demo: {selectedStation.DisplayName}");

            StatusLabel.Text = LocalizationResourceManager.Instance["StatusPreviewMode"];
            StatusLabel.TextColor = Colors.Orange;
        }
    }

    private void StopPreviewAndResumePrevious(Station station)
    {
        station.IsPreviewing = false;

        if (_stationBeforePreview != null)
        {
            PlayStation(_stationBeforePreview);
            _stationBeforePreview = null;
        }
        else
        {
            _audioService.Stop();
            StatusLabel.Text = LocalizationResourceManager.Instance["StatusReady"];
            StatusLabel.TextColor = Colors.Gray;
        }
    }

    private async void OnSearchResultAddClicked(object sender, Station station)
    {
        if (Stations.Any(s => s.Url == station.Url))
        {
            await DisplayAlert(
                LocalizationResourceManager.Instance["DialogInfoTitle"],
                LocalizationResourceManager.Instance["MsgStationExists"],
                "OK"
            );
            return;
        }

        Stations.Add(station);
        _stationManager.SaveCustomStation(station);

        string successTitle = LocalizationResourceManager.Instance["DialogSuccessTitle"];
        string successMsg = string.Format(
            LocalizationResourceManager.Instance["MsgStationAdded"],
            station.DisplayName
        );
        await DisplayAlert(successTitle, successMsg, "OK");
    }

    private void OnSearchResultsCloseClicked(object sender, EventArgs e)
    {
        SearchStationResultsOverlay.IsOpen = false;

        var list = SearchStationResultsOverlay.CurrentStations;
        var previewingStation = list?.FirstOrDefault(s => s.IsPreviewing);

        if (previewingStation != null)
        {
            StopPreviewAndResumePrevious(previewingStation);
        }
    }
}
