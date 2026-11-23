using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Clubes.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Clubes.Web.Controllers;

[Route("conta")]
public sealed class ContaController(IHttpClientFactory clients, IConfiguration configuration) : Controller
{
    private HttpClient Api => clients.CreateClient("api");

    [HttpGet("entrar")]
    public async Task<IActionResult> Entrar(CancellationToken ct)
    {
        var model = new LoginViewModel();
        try { model.Clubes = await Api.GetFromJsonAsync<List<ClubeOption>>("api/clubes", ct) ?? []; }
        catch (HttpRequestException) { model.Erro = "A API não está disponível. Inicie Clubes.Api e tente novamente."; }
        return View(model);
    }

    [HttpGet("criar-superadmin")]
    public IActionResult CriarSuperAdmin() => View(new BootstrapViewModel());

    [HttpPost("criar-superadmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarSuperAdmin(BootstrapViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var setupKey = configuration["Bootstrap:SetupKey"];
        if (string.IsNullOrWhiteSpace(setupKey))
        {
            model.Erro = "A configuração inicial não está disponível. Feche e reabra o iniciar-local.cmd para carregar a configuração de demonstração.";
            return View(model);
        }

        try
        {
            var response = await Api.PostAsJsonAsync("api/auth/bootstrap", new
            {
                setupKey,
                cpf = model.Cpf,
                nome = model.Nome,
                email = model.Email,
                senha = model.Senha
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                model.Erro = await GetError(response, ct);
                return View(model);
            }

            model.Criado = true;
            model.Senha = "";
            model.ConfirmarSenha = "";
            return View(model);
        }
        catch (HttpRequestException)
        {
            model.Erro = "Não foi possível conectar à API. Confira se a janela Clubes API continua aberta.";
            return View(model);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            model.Erro = "A API demorou para responder. Tente novamente.";
            return View(model);
        }
    }

    [HttpPost("entrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(LoginViewModel model, CancellationToken ct)
    {
        try
        {
            var response = await Api.PostAsJsonAsync("api/auth/login", new { cpf = model.Cpf, senha = model.Senha, clubeId = model.ClubeId }, ct);
            if (!response.IsSuccessStatusCode)
            {
                model.Erro = await GetError(response, ct);
                model.Clubes = await Api.GetFromJsonAsync<List<ClubeOption>>("api/clubes", ct) ?? [];
                return View(model);
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = payload.RootElement;
            var token = root.GetProperty("accessToken").GetString()!;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, root.GetProperty("userId").GetInt32().ToString()),
                new(ClaimTypes.Name, root.GetProperty("name").GetString() ?? "Usuário"),
                new(ClaimTypes.Role, root.GetProperty("role").GetString() ?? "Associado"),
                new("access_token", token)
            };
            if (root.TryGetProperty("activeClubId", out var clubId) && clubId.ValueKind == JsonValueKind.Number) claims.Add(new("club_id", clubId.GetInt32().ToString()));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
            return RedirectToAction("Index", "Home");
        }
        catch (HttpRequestException) { model.Erro = "Não foi possível conectar à API."; }
        model.Clubes = await Api.GetFromJsonAsync<List<ClubeOption>>("api/clubes", ct) ?? [];
        return View(model);
    }

    [HttpPost("sair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        try
        {
            var api = Api; api.DefaultRequestHeaders.Authorization = new("Bearer", User.FindFirst("access_token")?.Value);
            await api.PostAsync("api/auth/logout", null);
        }
        catch (HttpRequestException) { }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }

    private static async Task<string> GetError(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("message", out var message)) return message.GetString() ?? "Não foi possível concluir a operação.";
            if (doc.RootElement.TryGetProperty("detail", out var detail)) return detail.GetString() ?? "A API não conseguiu concluir a operação.";
            if (doc.RootElement.TryGetProperty("title", out var title)) return title.GetString() ?? "A API não conseguiu concluir a operação.";
        }
        catch (JsonException) { }
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Conflict => "O superadmin inicial já foi criado. Volte à tela de entrada.",
            System.Net.HttpStatusCode.ServiceUnavailable => "API temporariamente indisponível ou configuração de instalação ausente.",
            System.Net.HttpStatusCode.InternalServerError => "A API encontrou um erro ao salvar. Confira a mensagem na janela Clubes API e tente novamente.",
            _ => "A API recusou os dados informados. Confira o CPF, e-mail e senha."
        };
    }
}
