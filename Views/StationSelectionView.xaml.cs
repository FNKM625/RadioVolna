using System.Collections;
using RadioVolna;

namespace RadioVolna.Views;

public partial class StationSelectionView : ContentView
{
    // Zdarzenia, na które będzie nasłuchiwać MainPage
    public event EventHandler<Station>? StationSelected;
    public event EventHandler<Station>? FavoriteClicked;
    public event EventHandler<Station>? DeleteClicked;
    public event EventHandler? CloseRequested;

    public StationSelectionView()
    {
        InitializeComponent();
    }

    // Właściwość do ustawienia źródła danych z poziomu MainPage
    public IEnumerable ItemsSource
    {
        get => StationsList.ItemsSource;
        set => StationsList.ItemsSource = value;
    }

    public void ClearSelection()
    {
        StationsList.SelectedItem = null;
    }

    private void OnStationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Station selectedStation)
        {
            StationSelected?.Invoke(this, selectedStation);
        }
    }

    private void OnFavoriteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
        {
            FavoriteClicked?.Invoke(this, station);
        }
    }

    private void OnCloseListClicked(object sender, EventArgs e)
    {
        if (EditModeSwitch != null)
            EditModeSwitch.IsToggled = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
        this.IsVisible = false;
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
        {
            DeleteClicked?.Invoke(this, station);
        }
    }

    private void OnWindowContentTapped(object sender, EventArgs e) { }
}
