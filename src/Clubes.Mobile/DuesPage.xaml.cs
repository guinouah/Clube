using Clubes.Mobile.Core.Services;
using Clubes.Mobile.Core.ViewModels;

namespace Clubes.Mobile;

public partial class DuesPage : ContentPage
{
    private readonly IClubesApi _api;
    private readonly DuesViewModel _viewModel;
    public DuesPage(IClubesApi api, DuesViewModel viewModel) { _api = api; _viewModel = viewModel; InitializeComponent(); BindingContext = viewModel; }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _api.SetAccessToken(await SecureStorage.Default.GetAsync("access_token"));
        var clubId = int.TryParse(await SecureStorage.Default.GetAsync("active_club_id"), out var id) ? id : (int?)null;
        var personId = int.TryParse(await SecureStorage.Default.GetAsync("person_id"), out var person) ? person : (int?)null;
        await _viewModel.LoadAsync(clubId, personId);
    }
}
