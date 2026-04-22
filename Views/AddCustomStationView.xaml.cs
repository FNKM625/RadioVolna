namespace RadioVolna.Views;

public partial class AddCustomStationView : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(AddCustomStationView),
        false
    );

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string StationName => NameEntry.Text?.Trim() ?? string.Empty;
    public string StationUrl => UrlEntry.Text?.Trim() ?? string.Empty;
    public string StationEmoji => EmojiEntry.Text?.Trim() ?? string.Empty;

    public event EventHandler? PreviewClicked;
    public event EventHandler? SaveClicked;
    public event EventHandler? CancelClicked;

    public AddCustomStationView()
    {
        InitializeComponent();
    }

    public void ClearForm()
    {
        NameEntry.Text = string.Empty;
        UrlEntry.Text = string.Empty;
        EmojiEntry.Text = string.Empty;
    }

    private void OnPreviewClicked(object sender, EventArgs e) =>
        PreviewClicked?.Invoke(this, EventArgs.Empty);

    private void OnSaveClicked(object sender, EventArgs e) =>
        SaveClicked?.Invoke(this, EventArgs.Empty);

    private void OnCancelClicked(object sender, EventArgs e)
    {
        OnWindowContentTapped(this, EventArgs.Empty);
        CancelClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowContentTapped(object sender, EventArgs e)
    {
        NameEntry?.Unfocus();
        UrlEntry?.Unfocus();
        EmojiEntry?.Unfocus();
        if (NameEntry != null)
        {
            NameEntry.IsEnabled = false;
            NameEntry.IsEnabled = true;
        }
        if (UrlEntry != null)
        {
            UrlEntry.IsEnabled = false;
            UrlEntry.IsEnabled = true;
        }
        if (EmojiEntry != null)
        {
            EmojiEntry.IsEnabled = false;
            EmojiEntry.IsEnabled = true;
        }
    }
}
