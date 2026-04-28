using Microsoft.Maui.Controls;

namespace RadioVolna.Views;

public partial class AddStationMenuView : ContentView
{
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

    public event EventHandler? AddCustomClicked;
    public event EventHandler? SearchClicked;

    public AddStationMenuView()
    {
        InitializeComponent();
    }

    private void OnAddCustomClicked(object sender, EventArgs e)
    {
        IsOpen = false;
        AddCustomClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchClicked(object sender, EventArgs e)
    {
        IsOpen = false;
        SearchClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        IsOpen = false;
    }

    private void OnWindowContentTapped(object sender, EventArgs e) { }
}
