using System.Net.Http.Json;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile;

public partial class MorePage : ContentPage
{
    private readonly IClubesApi _api;
    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    public MorePage(IClubesApi api, HttpClient http, OfflineStore store)
    {
        _api = api; _http = http; _store = store; InitializeComponent();
        ManifestTypePicker.ItemsSource = new[] { "Solicitação", "Reclamação" };
        StartDate.Date = DateTime.Today.AddDays(1); EndDate.Date = DateTime.Today.AddDays(1);
        StartTime.Time = new TimeSpan(9, 0, 0); EndTime.Time = new TimeSpan(10, 0, 0);
        DependentBirthDate.MaximumDate = DateTime.Today;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!int.TryParse(await SecureStorage.Default.GetAsync("active_club_id"), out var clubId)) { ActionStatus.Text = "Entre na sua conta para enviar pedidos."; return; }
        _api.SetAccessToken(await SecureStorage.Default.GetAsync("access_token"));
        SpacePicker.ItemsSource = (await _api.GetSpacesAsync(clubId)).ToList();
        await LoadProfileAndDependentsAsync(clubId);
        await LoadRequestHistoryAsync(clubId);
    }
    private async void OnReserveClicked(object? sender, EventArgs e)
    {
        var active = await GetActiveClubAsync();
        if (!active.IsActive || SpacePicker.SelectedItem is not SpaceDto space || string.IsNullOrWhiteSpace(PurposeEntry.Text)) { ActionStatus.Text = "Entre e selecione o espaço e informe a finalidade."; return; }
        var clubId = active.ClubId;
        var start = (StartDate.Date ?? DateTime.Today).Add(StartTime.Time ?? TimeSpan.Zero); var end = (EndDate.Date ?? DateTime.Today).Add(EndTime.Time ?? TimeSpan.Zero);
        if (end <= start || start <= DateTime.Now) { ActionStatus.Text = "Escolha um intervalo futuro válido."; return; }
        var dependentId = (DependentPicker.SelectedItem as DependentDisplay)?.Id;
        var payload = System.Text.Json.JsonSerializer.Serialize(new { idempotencyKey = Guid.NewGuid(), espacoId = space.Id, finalidade = PurposeEntry.Text.Trim(), inicioUtc = start.ToUniversalTime(), fimUtc = end.ToUniversalTime(), dependenteId = dependentId });
        await SendOrQueueAsync(clubId, active.PersonId, $"api/clubes/{clubId}/reservas", payload, "Reserva");
    }
    private async void OnManifestClicked(object? sender, EventArgs e)
    {
        var active = await GetActiveClubAsync();
        if (!active.IsActive || ManifestTypePicker.SelectedIndex < 0 || string.IsNullOrWhiteSpace(SubjectEntry.Text) || string.IsNullOrWhiteSpace(DescriptionEditor.Text)) { ActionStatus.Text = "Informe o tipo, o assunto e a descrição."; return; }
        var clubId = active.ClubId;
        var payload = System.Text.Json.JsonSerializer.Serialize(new { tipo = ManifestTypePicker.SelectedIndex + 1, assunto = SubjectEntry.Text.Trim(), descricao = DescriptionEditor.Text.Trim() });
        await SendOrQueueAsync(clubId, active.PersonId, $"api/clubes/{clubId}/solicitacoes", payload, "Mensagem");
    }
    private async void OnAddDependentClicked(object? sender, EventArgs e)
    {
        var active = await GetActiveClubAsync();
        if (!active.IsActive || string.IsNullOrWhiteSpace(DependentNameEntry.Text) || string.IsNullOrWhiteSpace(DependentKinshipEntry.Text)) { ActionStatus.Text = "Informe o nome e o parentesco do dependente."; return; }
        try
        {
            using var response = await _http.PostAsJsonAsync($"api/clubes/{active.ClubId}/dependentes", new { nome = DependentNameEntry.Text.Trim(), dataNascimento = DependentBirthDate.Date?.ToString("yyyy-MM-dd"), parentesco = DependentKinshipEntry.Text.Trim() });
            ActionStatus.Text = response.IsSuccessStatusCode ? "Dependente cadastrado." : $"A API não aceitou o cadastro: {await response.Content.ReadAsStringAsync()}";
            if (response.IsSuccessStatusCode) await LoadProfileAndDependentsAsync(active.ClubId);
        }
        catch (HttpRequestException) { ActionStatus.Text = "Sem conexão. Cadastre dependentes quando estiver online."; }
    }

    private async void OnSaveInterestsClicked(object? sender, EventArgs e)
    {
        var active = await GetActiveClubAsync();
        if (!active.IsActive) { ActionStatus.Text = "Entre para salvar seus interesses."; return; }
        var names = (InterestsEntry.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        try
        {
            using var response = await _http.PutAsJsonAsync($"api/clubes/{active.ClubId}/interesses/meus", new { nomes = names });
            ActionStatus.Text = response.IsSuccessStatusCode ? "Interesses atualizados." : $"A API não aceitou os interesses: {await response.Content.ReadAsStringAsync()}";
        }
        catch (HttpRequestException) { ActionStatus.Text = "Conecte-se à API para atualizar interesses."; }
    }

    private async void OnSaveDueDayClicked(object? sender, EventArgs e)
    {
        var active = await GetActiveClubAsync();
        if (!active.IsActive || DueDayPicker.SelectedItem is not int day) { ActionStatus.Text = "Entre e escolha um vencimento disponível."; return; }
        try
        {
            using var response = await _http.PutAsJsonAsync($"api/clubes/{active.ClubId}/mensalidades/minha-data-vencimento", new { dia = day });
            ActionStatus.Text = response.IsSuccessStatusCode ? $"Vencimento atualizado para o dia {day}." : $"A API não aceitou a data: {await response.Content.ReadAsStringAsync()}";
        }
        catch (HttpRequestException) { ActionStatus.Text = "Conecte-se à API para alterar o vencimento."; }
    }
    private async Task<(bool IsActive, int ClubId, int PersonId)> GetActiveClubAsync()
    {
        var id = await SecureStorage.Default.GetAsync("active_club_id");
        if (!int.TryParse(id, out var clubId) || !int.TryParse(await SecureStorage.Default.GetAsync("person_id"), out var personId)) return (false, 0, 0);
        _api.SetAccessToken(await SecureStorage.Default.GetAsync("access_token")); return (true, clubId, personId);
    }
    private async Task SendOrQueueAsync(int clubId, int personId, string endpoint, string json, string itemName)
    {
        try
        {
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(endpoint, content);
            if (response.IsSuccessStatusCode) { ActionStatus.Text = itemName == "Reserva" ? "Pedido enviado e pendente de aprovação. A API validará os conflitos." : "Mensagem recebida pela API."; await LoadRequestHistoryAsync(clubId); return; }
            ActionStatus.Text = $"A API não aceitou o pedido: {await response.Content.ReadAsStringAsync()}";
        }
        catch (HttpRequestException)
        {
            var queued = await _api.QueueActionAsync(clubId, personId, endpoint, json);
            ActionStatus.Text = $"{itemName} {queued.Estado.ToLowerInvariant()} neste aparelho. A reserva ainda não está confirmada.";
            await LoadRequestHistoryAsync(clubId);
        }
    }

    private async Task LoadRequestHistoryAsync(int clubId)
    {
        RequestsList.Children.Clear();
        try
        {
            var reservations = await _http.GetFromJsonAsync<List<ReservationDisplay>>($"api/clubes/{clubId}/reservas") ?? [];
            foreach (var reservation in reservations) RequestsList.Children.Add(StatusCard($"Reserva · {reservation.Espaco}", $"{reservation.InicioUtc.ToLocalTime():dd/MM/yyyy HH:mm} · {reservation.Situacao}"));
            var messages = await _http.GetFromJsonAsync<List<ManifestDisplay>>($"api/clubes/{clubId}/solicitacoes") ?? [];
            foreach (var message in messages) RequestsList.Children.Add(StatusCard($"{message.Tipo} · {message.Assunto}", message.Situacao));
        }
        catch (HttpRequestException) { }
        var ownerId = int.TryParse(await SecureStorage.Default.GetAsync("person_id"), out var personId) ? personId : 0;
        foreach (var pending in await _store.GetActionsByClubAsync(clubId, ownerId))
            RequestsList.Children.Add(StatusCard(pending.Endpoint.Contains("reservas") ? "Reserva salva neste aparelho" : "Mensagem salva neste aparelho", $"{pending.Status} · {pending.QueuedUtc.ToLocalTime():dd/MM HH:mm}" + (string.IsNullOrWhiteSpace(pending.Error) ? "" : $" · {pending.Error}")));
        if (RequestsList.Children.Count == 0) RequestsList.Children.Add(new Label { Text = "Nenhum pedido enviado ainda.", FontSize = 11, TextColor = Color.FromArgb("#87928D") });
    }

    private async Task LoadProfileAndDependentsAsync(int clubId)
    {
        try
        {
            var profile = await _http.GetFromJsonAsync<ProfileDisplay>("api/auth/me");
            if (profile is not null) ProfileLabel.Text = $"{profile.NomeCompleto} · CPF {profile.Cpf} · {profile.Perfil}";
            var dependents = await _http.GetFromJsonAsync<List<DependentDisplay>>($"api/clubes/{clubId}/dependentes") ?? [];
            DependentPicker.ItemsSource = dependents;
            DependentsList.Children.Clear();
            foreach (var dependent in dependents) DependentsList.Children.Add(new Label { Text = $"{dependent.NomeCompleto} · {dependent.Parentesco}" + (dependent.DataNascimento is DateOnly birthday ? $" · {birthday:dd/MM/yyyy}" : ""), FontSize = 11, TextColor = Color.FromArgb("#42584F") });
            if (dependents.Count == 0) DependentsList.Children.Add(new Label { Text = "Nenhum dependente cadastrado.", FontSize = 11, TextColor = Color.FromArgb("#87928D") });
            var interests = await _http.GetFromJsonAsync<List<InterestDisplay>>($"api/clubes/{clubId}/interesses/meus") ?? [];
            InterestsEntry.Text = string.Join(", ", interests.Select(item => item.Nome));
            var dueDay = await _http.GetFromJsonAsync<DueDayDisplay>($"api/clubes/{clubId}/mensalidades/minha-data-vencimento");
            DueDayPicker.ItemsSource = dueDay?.Opcoes ?? [];
            if (dueDay is not null) DueDayPicker.SelectedItem = dueDay.DiaVencimento;
        }
        catch (HttpRequestException) { ProfileLabel.Text = "Dados e dependentes serão carregados ao conectar."; }
    }

    private static Border StatusCard(string title, string details)
    {
        var stack = new VerticalStackLayout { Spacing = 3 };
        stack.Children.Add(new Label { Text = title, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#18312D") });
        stack.Children.Add(new Label { Text = details, FontSize = 10, TextColor = Color.FromArgb("#71817B") });
        return new Border { Padding = 12, Stroke = Color.FromArgb("#E3EAE6"), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }, BackgroundColor = Colors.White, Content = stack };
    }

    private sealed record ReservationDisplay(Guid Id, string Espaco, string Situacao, DateTime InicioUtc);
    private sealed record ManifestDisplay(Guid Id, string Tipo, string Assunto, string Situacao);
    private sealed record ProfileDisplay(string NomeCompleto, string Cpf, string? Perfil);
    private sealed record DependentDisplay(int Id, string NomeCompleto, DateOnly? DataNascimento, string Parentesco);
    private sealed record InterestDisplay(int InteresseId, string Nome);
    private sealed record DueDayDisplay(int DiaVencimento, List<int> Opcoes);
}
