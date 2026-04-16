namespace RadioVolna.Views;

public partial class SearchStationView : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(SearchStationView),
        false
    );

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string SearchName => NameEntry.Text?.Trim() ?? string.Empty;
    public string SearchCountry => CountryEntry.Text?.Trim() ?? string.Empty;
    public string SearchTags => TagsEntry.Text?.Trim() ?? string.Empty;

    public event EventHandler? SearchClicked;
    public event EventHandler? CancelClicked;

    public SearchStationView()
    {
        InitializeComponent();
    }

    public void Reset()
    {
        NameEntry.Text = string.Empty;
        CountryEntry.Text = string.Empty;
        TagsEntry.Text = string.Empty;
    }

    private void OnSearchClicked(object sender, EventArgs e) =>
        SearchClicked?.Invoke(this, EventArgs.Empty);

    private void OnCancelClicked(object sender, EventArgs e) =>
        CancelClicked?.Invoke(this, EventArgs.Empty);
}
