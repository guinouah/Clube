using Clubes.Mobile.Core.ViewModels;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile;

public partial class EventsPage : ContentPage
{
    private readonly EventsViewModel _viewModel;
    private readonly IClubesApi _api;
    public EventsPage(EventsViewModel viewModel, IClubesApi api) { _viewModel = viewModel; _api = api; InitializeComponent(); BindingContext = viewModel; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var token = await SecureStorage.Default.GetAsync("access_token");
        _api.SetAccessToken(token);
        var clubId = int.TryParse(await SecureStorage.Default.GetAsync("active_club_id"), out var id) ? id : (int?)null;
        await _viewModel.LoadAsync(clubId);
    }
}
