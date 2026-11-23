using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Clubes.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Clubes.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("admin/clubes")]
public sealed class ClubesAdminController(IHttpClientFactory clients) : Controller
{
    private HttpClient Api
    {
        get
        {
            var api = clients.CreateClient("api");
            api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", User.FindFirst("access_token")?.Value);
            return api;
        }
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { ViewBag.Clubes = await clients.CreateClient("api").GetFromJsonAsync<List<ClubeOption>>("api/clubes", ct) ?? []; }
        catch (HttpRequestException) { ViewBag.Error = "API indisponível."; ViewBag.Clubes = new List<ClubeOption>(); }
        return View();
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Novo(CancellationToken ct) => View("Formulario", await LoadFormAsync(new ClubeFormViewModel(), ct));

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(ClubeFormViewModel model, CancellationToken ct)
    {
        await LoadOptionsAsync(model, ct);
        if (!ModelState.IsValid) return View("Formulario", model);
        try
        {
            var response = await Api.PostAsJsonAsync("api/clubes", new { model.Nome, model.Sigla, model.Endereco, model.Bairro, model.Cidade, model.Uf, model.Telefone, model.Email }, ct);
            if (!response.IsSuccessStatusCode) { model.Erro = await ErrorMessage(response, ct); return View("Formulario", model); }
            TempData["Success"] = "Clube criado. Os associados existentes foram vinculados conforme a regra global do projeto.";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException) { model.Erro = "Não foi possível conectar à API."; return View("Formulario", model); }
    }

    [HttpPost("{id:int}/acessar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acessar(int id, CancellationToken ct)
    {
        using var response = await Api.PostAsJsonAsync("api/auth/selecionar-clube", new { clubeId = id }, ct);
        if (!response.IsSuccessStatusCode) { TempData["Error"] = "Não foi possível ativar esse clube."; return RedirectToAction(nameof(Index)); }
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var root = document.RootElement;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, root.GetProperty("userId").GetInt32().ToString()),
            new(ClaimTypes.Name, root.GetProperty("name").GetString() ?? "Usuário"),
            new(ClaimTypes.Role, root.GetProperty("role").GetString() ?? "Associado"),
            new("access_token", root.GetProperty("accessToken").GetString()!),
            new("club_id", id.ToString())
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("municipios")]
    [AllowAnonymous]
    public async Task<IActionResult> Municipios(string uf, CancellationToken ct)
    {
        var api = clients.CreateClient("api");
        try { return Json(await api.GetFromJsonAsync<JsonElement>($"api/localidades/estados/{Uri.EscapeDataString(uf)}/municipios", ct)); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Lista de municípios temporariamente indisponível." }); }
    }

    private async Task<ClubeFormViewModel> LoadFormAsync(ClubeFormViewModel model, CancellationToken ct)
    {
        await LoadOptionsAsync(model, ct);
        if (!string.IsNullOrWhiteSpace(model.Uf))
        {
            var api = clients.CreateClient("api");
            try
            {
                var municipalities = await api.GetFromJsonAsync<List<MunicipioDto>>($"api/localidades/estados/{model.Uf}/municipios", ct) ?? [];
                model.Municipios = municipalities.Select(row => row.Nome).ToList();
            }
            catch (HttpRequestException) { }
        }
        return model;
    }
    private async Task LoadOptionsAsync(ClubeFormViewModel model, CancellationToken ct)
    {
        try { model.Estados = (await clients.CreateClient("api").GetFromJsonAsync<List<EstadoOption>>("api/localidades/estados", ct) ?? []).ToList(); }
        catch (HttpRequestException) { model.Estados = []; model.Erro ??= "Não foi possível carregar os estados brasileiros. Verifique a API."; }
    }
    private static async Task<string> ErrorMessage(HttpResponseMessage response, CancellationToken ct)
    {
        try { using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "Não foi possível salvar o clube." : "Não foi possível salvar o clube."; }
        catch { return "Não foi possível salvar o clube."; }
    }
    private sealed record MunicipioDto(long CodigoIbge, string Nome);
}
