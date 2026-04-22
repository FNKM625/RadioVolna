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

    private void OnCancelClicked(object sender, EventArgs e)
    {
        OnWindowContentTapped(this, EventArgs.Empty);
        CancelClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowContentTapped(object sender, EventArgs e)
    {
        NameEntry?.Unfocus();
        CountryEntry?.Unfocus();
        TagsEntry?.Unfocus();
        if (NameEntry != null)
        {
            NameEntry.IsEnabled = false;
            NameEntry.IsEnabled = true;
        }
        if (CountryEntry != null)
        {
            CountryEntry.IsEnabled = false;
            CountryEntry.IsEnabled = true;
        }
        if (TagsEntry != null)
        {
            TagsEntry.IsEnabled = false;
            TagsEntry.IsEnabled = true;
        }
    }
}
