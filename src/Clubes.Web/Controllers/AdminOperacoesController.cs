using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clubes.Web.Controllers;

[Authorize]
public sealed class AdminOperacoesController(IHttpClientFactory clients, Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory tempData) : Controller
{
    [HttpPost("painel/criar-evento")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public Task<IActionResult> CriarEvento(string nome, string descricao, DateTime inicioUtc, DateTime fimUtc, string local, bool publicoGeral, bool beneficente, int? espacoId, CancellationToken ct)
    {
        var body = new { nome, descricao, inicioUtc = DateTime.SpecifyKind(inicioUtc, DateTimeKind.Local).ToUniversalTime(), fimUtc = DateTime.SpecifyKind(fimUtc, DateTimeKind.Local).ToUniversalTime(), local, publicoGeral, beneficente, espacoId };
        return SendAsync("eventos", $"eventos", body, ct);
    }

    [HttpPost("painel/criar-espaco")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public Task<IActionResult> CriarEspaco(string nome, string descricao, CancellationToken ct) => SendAsync("espacos", "espacos", new { nome, descricao }, ct);

    [HttpPost("painel/definir-valor")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public Task<IActionResult> DefinirValor(int categoria, decimal valor, DateOnly vigenteDesde, CancellationToken ct) => SendAsync("mensalidades", "mensalidades/valores", new { categoria, valor, vigenteDesde }, ct, HttpMethod.Put);

    [HttpPost("painel/gerar-mensalidades")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public Task<IActionResult> GerarMensalidades(int ano, int mes, DateOnly vencimento, CancellationToken ct) => SendAsync("mensalidades", "mensalidades/gerar", new { ano, mes, vencimento }, ct);

    [HttpPost("painel/atribuir-perfil")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public Task<IActionResult> AtribuirPerfil(string cpf, string nome, string email, string senha, int perfil, CancellationToken ct) => SendAsync("associados", "usuarios", new { cpf, nome, email, senha, perfil }, ct, HttpMethod.Post, clubScoped: true);

    [HttpPost("painel/atualizar-solicitacao")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public Task<IActionResult> AtualizarSolicitacao(Guid id, int situacao, CancellationToken ct) => SendAsync("solicitacoes", $"solicitacoes/{id}/situacao", new { situacao }, ct, HttpMethod.Patch);

    private async Task<IActionResult> SendAsync(string module, string suffix, object body, CancellationToken ct, HttpMethod? method = null, bool clubScoped = true)
    {
        var clubId = User.FindFirst("club_id")?.Value;
        if (clubScoped && string.IsNullOrWhiteSpace(clubId)) return Forbid();
        using var api = clients.CreateClient("api"); api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", User.FindFirst("access_token")?.Value);
        var endpoint = clubScoped ? $"api/clubes/{clubId}/{suffix}" : suffix;
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, endpoint) { Content = JsonContent.Create(body) };
        try
        {
            using var response = await api.SendAsync(request, ct);
            var temp = tempData.GetTempData(HttpContext);
            temp[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode ? "Alteração salva." : $"A API recusou os dados: {await response.Content.ReadAsStringAsync(ct)}";
        }
        catch (HttpRequestException) { tempData.GetTempData(HttpContext)["Error"] = "API indisponível. A alteração não foi salva."; }
        return RedirectToAction("Module", "Home", new { id = module });
    }
}
