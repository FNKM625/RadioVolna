namespace RadioVolna.Views;

public partial class AddCustomStationView : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(AddCustomStationView), // Zmiana nazwy tutaj!
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

    private void OnCancelClicked(object sender, EventArgs e) =>
        CancelClicked?.Invoke(this, EventArgs.Empty);
}
