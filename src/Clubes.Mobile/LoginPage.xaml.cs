using System.Net.Http.Json;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile;

public partial class LoginPage : ContentPage
{
    private readonly IClubesApi _api;
    private readonly OfflineStore _store;
    private readonly HttpClient _http;
    private readonly Clubes.Mobile.Services.OfflineSyncCoordinator _offlineSync;
    private List<ClubOption> _clubs = [];

    public LoginPage(IClubesApi api, OfflineStore store, HttpClient http, Clubes.Mobile.Services.OfflineSyncCoordinator offlineSync)
    {
        InitializeComponent(); _api = api; _store = store; _http = http; _offlineSync = offlineSync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _store.InitializeAsync();
            _clubs = await _http.GetFromJsonAsync<List<ClubOption>>("api/clubes") ?? [];
            ClubPicker.ItemsSource = _clubs;
            var club = await SecureStorage.Default.GetAsync("active_club_id");
            if (int.TryParse(club, out var id)) ClubPicker.SelectedItem = _clubs.FirstOrDefault(row => row.Id == id);
            var token = await SecureStorage.Default.GetAsync("access_token"); _api.SetAccessToken(token);
            StatusLabel.Text = token is null ? "Entre enquanto houver conexão para habilitar o uso offline." : "Sessão guardada. Os dados podem estar desatualizados até sincronizar.";
        }
        catch (HttpRequestException) { StatusLabel.Text = "API indisponível. Você ainda pode consultar os dados em cache."; }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var club = ClubPicker.SelectedItem as ClubOption;
        var result = await _api.LoginAsync(CpfEntry.Text ?? "", PasswordEntry.Text ?? "", club?.Id);
        StatusLabel.Text = result.Message;
        if (!result.Success || result.Token is null) return;
        await SecureStorage.Default.SetAsync("access_token", result.Token);
        await SecureStorage.Default.SetAsync("person_id", result.PersonId.ToString());
        if (result.ClubId is int id) await SecureStorage.Default.SetAsync("active_club_id", id.ToString());
        _api.SetAccessToken(result.Token);
        await _offlineSync.SynchronizeNowAsync();
        await Shell.Current.GoToAsync("//inicio");
    }

    private async void OnRegisterClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new RegisterPage(_api, _http));

    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        var token = await SecureStorage.Default.GetAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) { StatusLabel.Text = "Entre online para sincronizar."; return; }
        _api.SetAccessToken(token);
        if (int.TryParse(await SecureStorage.Default.GetAsync("active_club_id"), out var clubId) && int.TryParse(await SecureStorage.Default.GetAsync("person_id"), out var personId))
        {
            var result = await _api.SynchronizeAsync(clubId, personId); StatusLabel.Text = $"{result.Mensagem} Enviadas: {result.Enviados}; com pendência: {result.Falhas}.";
        }
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var token = await SecureStorage.Default.GetAsync("access_token");
        if (token is not null)
        {
            _api.SetAccessToken(token);
            try { await _http.PostAsync("api/auth/logout", null); } catch (HttpRequestException) { }
        }
        SecureStorage.Default.Remove("access_token"); SecureStorage.Default.Remove("active_club_id"); SecureStorage.Default.Remove("person_id"); _api.SetAccessToken(null);
        StatusLabel.Text = "Sessão encerrada neste aparelho.";
    }

    private sealed record ClubOption(int Id, string Nome, string Sigla)
    {
        public override string ToString() => $"{Nome} ({Sigla})";
    }
}
