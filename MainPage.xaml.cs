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
    private Station _stationBeforePreview;

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
        // 1. Ładowanie domyślnych stacji (np. z API lub pliku bazy)
        var loadedStations = await _stationService.GetStationsAsync();

        if (loadedStations.Count == 0)
        {
            string title = LocalizationResourceManager.Instance["TitleError"];
            string msg = LocalizationResourceManager.Instance["MsgStationListError"];
            string ok = LocalizationResourceManager.Instance["BtnOk"];
            await DisplayAlert(title, msg, ok);
            return;
        }

        // 2. Scalanie domyślnych stacji z ulubionymi
        _stationManager.MergeWithFavorites(loadedStations, Stations);

        // NOWE: 3. Ładowanie własnych stacji użytkownika
        var customStations = _stationManager.LoadCustomStations();
        foreach (var customStation in customStations)
        {
            // Upewniamy się, że nie dodajemy duplikatów, jeśli URL już jest na liście
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
        // Wyświetlenie okna z pytaniem o potwierdzenie
        bool answer = await DisplayAlert(
            "Usuwanie stacji",
            $"Czy na pewno chcesz usunąć stację '{station.DisplayName}'?",
            "Tak",
            "Nie"
        );

        if (answer)
        {
            // 1. Usuwamy z pliku JSON (jeśli to stacja własna)
            _stationManager.DeleteCustomStation(station);

            // 2. Usuwamy z aktualnie wyświetlanej listy w aplikacji
            Stations.Remove(station);

            // Opcjonalnie: jeśli usunięta stacja była aktualnie odtwarzana, możemy zatrzymać radio
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
        // Otwiera menu (zmienia właściwość IsOpen, co wyświetla Grid w Twoim XAML)
        AddStationMenuOverlay.IsOpen = true;
    }

    private void OnAddCustomStationClicked(object sender, EventArgs e)
    {
        // Czyścimy formularz przed wyświetleniem i otwieramy overlay
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

    private void OnCustomStationPreviewClicked(object sender, EventArgs e)
    {
        string url = AddCustomStationOverlay.StationUrl;
        string name = AddCustomStationOverlay.StationName;

        if (string.IsNullOrWhiteSpace(url))
        {
            DisplayAlert("Błąd", "Podaj link do streamu, aby go przetestować.", "OK");
            return;
        }

        // Odtwarzanie próbne - używamy nazwy, a jeśli jej nie ma, wpisujemy domyślną
        _audioService.Play(url, string.IsNullOrWhiteSpace(name) ? "Test streamu..." : name);

        // Ręcznie symulujemy stan na interfejsie
        StatusLabel.Text = "Testowanie własnego linku...";
        StatusLabel.TextColor = Colors.LightBlue;
    }

    private void OnCustomStationSaveClicked(object sender, EventArgs e)
    {
        string name = AddCustomStationOverlay.StationName;
        string url = AddCustomStationOverlay.StationUrl;
        string emoji = AddCustomStationOverlay.StationEmoji;

        // 1. Walidacja nazwy (nie może być pusta)
        if (string.IsNullOrWhiteSpace(name))
        {
            DisplayAlert("Błąd", "Nazwa stacji jest wymagana.", "OK");
            return;
        }

        // 2. Walidacja URL
        bool isValidUrl =
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        if (!isValidUrl)
        {
            DisplayAlert(
                "Błąd",
                "Podany link jest niepoprawny. Upewnij się, że zawiera przedrostek http:// lub https:// i nie ma w nim spacji.",
                "OK"
            );
            return;
        }

        // Łączymy emoji z nazwą, jeśli podano
        string displayName = string.IsNullOrWhiteSpace(emoji) ? name : $"{emoji} {name}";

        // 3. Tworzenie nowej stacji
        var newStation = new Station
        {
            Name = displayName,
            DisplayName = displayName,
            Url = url,
            IsFavorite = false,
        };

        // 4. Dodanie stacji do listy obserwowalnej (pojawi się od razu w interfejsie)
        Stations.Add(newStation);

        _stationManager.SaveCustomStation(newStation);

        DisplayAlert("Sukces", $"Stacja '{displayName}' została dodana!", "OK");

        // Zamykamy overlay formularza
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

        // Minimum jedno pole musi być uzupełnione
        if (
            string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(country)
            && string.IsNullOrWhiteSpace(tags)
        )
        {
            await DisplayAlert("Błąd", "Wypełnij przynajmniej jedno pole wyszukiwania.", "OK");
            return;
        }

        SearchStationFilterOverlay.IsOpen = false;
        StatusLabel.Text = "Przeszukiwanie globalnej bazy...";
        StatusLabel.TextColor = Colors.LightBlue;

        // Pobieranie wyników z API
        var results = await _stationService.SearchRadioBrowserAsync(name, country, tags);

        // FILTROWANIE: Usuwamy z wyników stacje, które już mamy na głównej liście (po URL)
        var filteredResults = results
            .Where(apiStation => !Stations.Any(myStation => myStation.Url == apiStation.Url))
            .ToList();

        StatusLabel.Text = LocalizationResourceManager.Instance["StatusReady"];

        if (filteredResults.Count == 0)
        {
            await DisplayAlert(
                "Brak nowych stacji",
                "Wszystkie znalezione stacje są już na Twojej liście.",
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
            // JEŚLI JUŻ GRA DEMO TEJ STACJI -> ZATRZYMUJEMY I WRACAMY DO POPRZEDNIEJ
            StopPreviewAndResumePrevious(selectedStation);
        }
        else
        {
            // Zatrzymujemy inne demo, jeśli jakieś grało
            if (SearchStationResultsOverlay.CurrentStations != null)
            {
                foreach (var s in SearchStationResultsOverlay.CurrentStations)
                    s.IsPreviewing = false;
            }

            // Jeśli to pierwsze demo w tej sesji, zapamiętaj co grało normalnie
            if (_stationBeforePreview == null)
            {
                // Szukamy aktualnie grającej stacji po nazwie z labela
                _stationBeforePreview = Stations.FirstOrDefault(s =>
                    s.DisplayName == CurrentStationLabel.Text
                );
            }

            selectedStation.IsPreviewing = true;
            _audioService.Play(selectedStation.Url, $"Demo: {selectedStation.DisplayName}");

            StatusLabel.Text = "Tryb podglądu...";
            StatusLabel.TextColor = Colors.Orange;
        }
    }

    private void StopPreviewAndResumePrevious(Station station)
    {
        station.IsPreviewing = false;

        if (_stationBeforePreview != null)
        {
            // Wznawiamy stację, która grała wcześniej
            PlayStation(_stationBeforePreview);
            _stationBeforePreview = null; // Resetujemy pamięć
        }
        else
        {
            // Jeśli nic nie grało, po prostu zatrzymaj audio
            _audioService.Stop();
            StatusLabel.Text = LocalizationResourceManager.Instance["StatusReady"];
            StatusLabel.TextColor = Colors.Gray;
        }
    }

    private void OnSearchResultAddClicked(object sender, Station station)
    {
        // Sprawdzamy czy stacja o takim samym linku już jest w naszych ulubionych/własnych
        if (Stations.Any(s => s.Url == station.Url))
        {
            DisplayAlert("Info", "Ta stacja jest już na Twojej liście.", "OK");
            return;
        }

        // Dodanie stacji (używamy mechanizmu zapisu z poprzednich wiadomości)
        Stations.Add(station);
        _stationManager.SaveCustomStation(station); // Zapis do JSON

        DisplayAlert(
            "Sukces",
            $"Stacja '{station.DisplayName}' została dodana do Twojej listy!",
            "OK"
        );
    }

    private void OnSearchResultsCloseClicked(object sender, EventArgs e)
    {
        SearchStationResultsOverlay.IsOpen = false;

        // Resetujemy wszystkie stany previewing na liście
        var list = SearchStationResultsOverlay.CurrentStations;
        var previewingStation = list?.FirstOrDefault(s => s.IsPreviewing);

        if (previewingStation != null)
        {
            StopPreviewAndResumePrevious(previewingStation);
        }
    }
}
