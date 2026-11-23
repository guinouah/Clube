namespace Clubes.Mobile;

using Clubes.Mobile.Core.ViewModels;

public partial class MainPage : ContentPage
{
	private readonly DashboardViewModel _viewModel;

	public MainPage(DashboardViewModel viewModel)
    {
		_viewModel = viewModel;
        InitializeComponent();
		BindingContext = viewModel;
    }

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.RefreshAsync();
	}

    private async void OnEventsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//eventos");

    private async void OnDuesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//mensalidades");
}
