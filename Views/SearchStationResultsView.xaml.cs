using RadioVolna;

namespace RadioVolna.Views;

public partial class SearchStationResultsView : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(SearchStationResultsView),
        false
    );

    public IEnumerable<Station>? CurrentStations => ResultsList.ItemsSource as IEnumerable<Station>;

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public event EventHandler<Station>? PreviewClicked;
    public event EventHandler<Station>? AddClicked;
    public event EventHandler? CloseClicked;

    public SearchStationResultsView()
    {
        InitializeComponent();
    }

    public void SetItems(IEnumerable<Station> stations)
    {
        ResultsList.ItemsSource = stations;
    }

    private void OnPreviewClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
            PreviewClicked?.Invoke(this, station);
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Station station)
            AddClicked?.Invoke(this, station);
    }

    private void OnCloseClicked(object sender, EventArgs e) =>
        CloseClicked?.Invoke(this, EventArgs.Empty);

    private void OnWindowContentTapped(object sender, EventArgs e) { }
}
