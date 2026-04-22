using Microsoft.Maui.Controls;

namespace RadioVolna.Views;

public partial class AddStationMenuView : ContentView
{
    // BindableProperty pozwala na używanie IsOpen w bindowaniu XAML
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(AddStationMenuView),
        false
    );

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    // Zdarzenia, na które będziemy reagować w MainPage
    public event EventHandler? AddCustomClicked;
    public event EventHandler? SearchClicked;

    public AddStationMenuView()
    {
        InitializeComponent();
    }

    private void OnAddCustomClicked(object sender, EventArgs e)
    {
        IsOpen = false; // Zamknij menu
        AddCustomClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchClicked(object sender, EventArgs e)
    {
        IsOpen = false; // Zamknij menu
        SearchClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        IsOpen = false; // Po prostu zamknij menu
    }

    private void OnWindowContentTapped(object sender, EventArgs e) { }
}
