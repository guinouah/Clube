using System.Net.Http.Json;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile;

public partial class RegisterPage : ContentPage
{
    private readonly IClubesApi _api;
    private readonly HttpClient _http;
    private List<StateDto> _states = [];
    private List<ClubOption> _clubs = [];
    public RegisterPage(IClubesApi api, HttpClient http)
    {
        _api = api; _http = http; InitializeComponent();
        CategoryPicker.ItemsSource = new[] { "Fundador associado", "Convidado", "Simpatizante" };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _states = (await _api.GetStatesAsync()).ToList(); StatePicker.ItemsSource = _states;
            _clubs = await _http.GetFromJsonAsync<List<ClubOption>>("api/clubes") ?? []; ClubPicker.ItemsSource = _clubs;
        }
        catch (HttpRequestException) { StatusLabel.Text = "Conecte-se à internet para carregar estados, municípios e clubes oficiais."; }
    }
    private async void OnStateChanged(object? sender, EventArgs e)
    {
        CityPicker.ItemsSource = Array.Empty<string>(); CityPicker.IsEnabled = false;
        if (StatePicker.SelectedItem is not StateDto state) return;
        try { CityPicker.ItemsSource = (await _api.GetCitiesAsync(state.Sigla)).Select(row => row.Nome).ToList(); CityPicker.IsEnabled = true; CityPicker.Title = "Selecione o município"; }
        catch (HttpRequestException) { StatusLabel.Text = "Lista de municípios indisponível."; }
    }
    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (StatePicker.SelectedItem is not StateDto state || CityPicker.SelectedItem is not string city || ClubPicker.SelectedItem is not ClubOption club || CategoryPicker.SelectedIndex < 0)
        { StatusLabel.Text = "Preencha o estado, município, clube e categoria."; return; }
        if (string.IsNullOrWhiteSpace(NameEntry.Text) || string.IsNullOrWhiteSpace(CpfEntry.Text) || string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text) || PasswordEntry.Text.Length < 12)
        { StatusLabel.Text = "Informe seus dados e uma senha com pelo menos 12 caracteres."; return; }
        try
        {
            using var response = await _http.PostAsJsonAsync("api/auth/registro", new { cpf = CpfEntry.Text, nome = NameEntry.Text, email = EmailEntry.Text, telefone = PhoneEntry.Text ?? "", senha = PasswordEntry.Text, clubeId = club.Id, categoria = CategoryPicker.SelectedIndex + 1, justificativa = JustificationEditor.Text ?? "", endereco = AddressEntry.Text ?? "", bairro = NeighborhoodEntry.Text ?? "", cidade = city, uf = state.Sigla });
            if (!response.IsSuccessStatusCode) { var message = await response.Content.ReadAsStringAsync(); StatusLabel.Text = $"Não foi possível enviar: {message}"; return; }
            StatusLabel.Text = "Pedido enviado. O acesso será liberado após aprovação.";
            await DisplayAlertAsync("Pedido enviado", "Seu pedido está pendente de aprovação. Use este mesmo CPF para entrar depois que o clube aprovar.", "Entendi");
            await Navigation.PopModalAsync();
        }
        catch (HttpRequestException) { StatusLabel.Text = "Sem conexão. O cadastro inicial precisa ser enviado online."; }
    }
    private async void OnCloseClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
    private sealed record ClubOption(int Id, string Nome, string Sigla);
}
