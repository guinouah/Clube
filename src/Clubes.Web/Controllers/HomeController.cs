using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Clubes.Web.Models;
using Clubes.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clubes.Web.Controllers;

[Authorize]
public sealed class HomeController(IHttpClientFactory clients, ClubesApiClient apiClient) : Controller
{
    private static readonly IReadOnlyDictionary<string, (string Title, string Description, string Icon, string Role, string Endpoint)> Modules =
        new Dictionary<string, (string, string, string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["clubes"] = ("Clubes", "Dados e unidades da plataforma.", "♧", "Superadmin", "api/clubes"),
            ["associados"] = ("Associados", "Pessoas vinculadas ao clube selecionado.", "♙", "Presidência", "associados"),
            ["solicitacoes"] = ("Solicitações", "Pedidos de ingresso, solicitações e reclamações.", "✉", "Presidência", "solicitacoes"),
            ["eventos"] = ("Eventos", "Agenda e público dos próximos eventos.", "✳", "Clube", "eventos"),
            ["mensalidades"] = ("Mensalidades", "Situação das mensalidades e competências.", "R$", "Tesouraria", "mensalidades"),
            ["reservas"] = ("Reservas", "Pedidos de uso dos espaços e horários.", "▦", "Presidência", "reservas"),
            ["espacos"] = ("Espaços", "Espaços cadastrados para reserva.", "⌂", "Presidência", "espacos")
        };

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var status = await apiClient.GetStatusAsync(cancellationToken);
        return View(status);
    }

    [Route("painel/{id}")]
    public async Task<IActionResult> Module(string id, CancellationToken ct)
    {
        if (!Modules.TryGetValue(id, out var module)) return NotFound();
        if (id == "clubes") return User.IsInRole("SuperAdmin") ? RedirectToAction("Index", "ClubesAdmin") : Forbid();
        if (id == "associados" && !User.IsInRole("Presidente") && !User.IsInRole("SuperAdmin")) return Forbid();
        if (id == "solicitacoes" && !User.IsInRole("Presidente") && !User.IsInRole("SuperAdmin")) return Forbid();
        if (id == "reservas" && !User.IsInRole("Presidente") && !User.IsInRole("SuperAdmin")) return Forbid();
        if (id == "espacos" && !User.IsInRole("Presidente") && !User.IsInRole("SuperAdmin")) return Forbid();
        if (id == "eventos" && !User.IsInRole("Presidente") && !User.IsInRole("SuperAdmin")) return Forbid();
        if (id == "mensalidades" && !User.IsInRole("Tesoureiro") && !User.IsInRole("SuperAdmin")) return Forbid();

        var rows = new List<IReadOnlyDictionary<string, string>>(); string? error = null;
        var availableSpaces = new List<SpaceOption>();
        var clubId = User.FindFirst("club_id")?.Value;
        if (string.IsNullOrWhiteSpace(clubId)) error = "Selecione um clube para consultar os dados.";
        else
        {
            var endpoint = id == "mensalidades" ? $"api/clubes/{clubId}/mensalidades" : $"api/clubes/{clubId}/{module.Endpoint}";
            using var api = clients.CreateClient("api"); api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", User.FindFirst("access_token")?.Value);
            try
            {
                using var response = await api.GetAsync(endpoint, ct);
                if (!response.IsSuccessStatusCode) error = response.StatusCode == System.Net.HttpStatusCode.Forbidden ? "Seu perfil não pode consultar este módulo." : "A API não conseguiu carregar os dados.";
                else
                {
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in document.RootElement.EnumerateArray())
                        {
                            var row = item.EnumerateObject().ToDictionary(prop => Humanize(prop.Name), prop => Format(prop.Value));
                            if (id == "solicitacoes") row["Origem"] = "Atendimento";
                            rows.Add(row);
                        }
                    }
                }
                if (id == "solicitacoes" && response.IsSuccessStatusCode)
                {
                    using var ingress = await api.GetAsync($"api/clubes/{clubId}/pedidos-ingresso", ct);
                    if (ingress.IsSuccessStatusCode)
                    {
                        using var document = JsonDocument.Parse(await ingress.Content.ReadAsStringAsync(ct));
                        foreach (var item in document.RootElement.EnumerateArray())
                        {
                            var row = item.EnumerateObject().ToDictionary(prop => Humanize(prop.Name), prop => Format(prop.Value));
                            row["Origem"] = "Ingresso"; rows.Add(row);
                        }
                    }
                }
                if (id == "eventos" && response.IsSuccessStatusCode)
                {
                    using var spacesResponse = await api.GetAsync($"api/clubes/{clubId}/espacos", ct);
                    if (spacesResponse.IsSuccessStatusCode)
                        availableSpaces = await JsonSerializer.DeserializeAsync<List<SpaceOption>>(
                            await spacesResponse.Content.ReadAsStreamAsync(ct), new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? [];
                }
            }
            catch (HttpRequestException) { error = "API indisponível. Verifique se o serviço está aberto."; }
        }
        return View("Module", new ModulePageViewModel(id, module.Title, module.Description, module.Icon, module.Role, "", rows, error, availableSpaces));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [HttpPost("painel/avaliar-ingresso")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public Task<IActionResult> AvaliarIngresso(Guid id, bool aprovar, CancellationToken ct) => SendActionAsync($"api/clubes/{User.FindFirst("club_id")?.Value}/pedidos-ingresso/{id}", HttpMethod.Patch, new { aprovar }, "solicitacoes", ct);

    [HttpPost("painel/avaliar-reserva")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public Task<IActionResult> AvaliarReserva(Guid id, bool aprovar, CancellationToken ct) => SendActionAsync($"api/clubes/{User.FindFirst("club_id")?.Value}/reservas/{id}/avaliacao", HttpMethod.Patch, new { aprovar, observacao = "" }, "reservas", ct);

    [HttpPost("painel/registrar-pagamento")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public Task<IActionResult> RegistrarPagamento(Guid id, bool paga, CancellationToken ct) => SendActionAsync($"api/clubes/{User.FindFirst("club_id")?.Value}/mensalidades/{id}/pagamento", HttpMethod.Patch, new { paga, forma = "Painel" }, "mensalidades", ct);

    private async Task<IActionResult> SendActionAsync(string endpoint, HttpMethod method, object body, string module, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(User.FindFirst("club_id")?.Value)) return Forbid();
        using var api = clients.CreateClient("api"); api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", User.FindFirst("access_token")?.Value);
        using var request = new HttpRequestMessage(method, endpoint) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        try
        {
            using var response = await api.SendAsync(request, ct);
            TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode ? "Alteração salva." : $"A API recusou a alteração: {await response.Content.ReadAsStringAsync(ct)}";
        }
        catch (HttpRequestException) { TempData["Error"] = "API indisponível. A alteração não foi salva."; }
        return RedirectToAction(nameof(Module), new { id = module });
    }

    private static string Humanize(string value)
    {
        if (value.Equals("id", StringComparison.OrdinalIgnoreCase)) return "ID";
        return string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? " " + character : character.ToString())).Replace(" Id", " ID", StringComparison.OrdinalIgnoreCase);
    }
    private static string Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => "—", JsonValueKind.True => "Sim", JsonValueKind.False => "Não", JsonValueKind.String => value.GetString() ?? "—", _ => value.ToString()
    };
}
