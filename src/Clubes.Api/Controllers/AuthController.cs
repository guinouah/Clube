using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Clubes.Api.Data;
using Clubes.Api.Models;
using Clubes.Api.Security;
using Clubes.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Clubes.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(
    ClubesDbContext db,
    IPasswordHasher<Usuario> passwordHasher,
    IConfiguration configuration,
    LocalidadesService localidades) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap(BootstrapRequest request, CancellationToken cancellationToken)
    {
        var setupKey = configuration["Bootstrap:SetupKey"];
        if (string.IsNullOrWhiteSpace(setupKey))
        {
            return Problem("Configure a variável Bootstrap__SetupKey antes de criar o primeiro superadmin.", statusCode: 503);
        }

        if (!FixedTimeEquals(setupKey, request.SetupKey))
        {
            return Unauthorized(new { message = "Chave de instalação inválida." });
        }

        if (await db.Usuarios.AnyAsync(usuario => usuario.SuperAdmin, cancellationToken))
        {
            return Conflict(new { message = "O superadmin inicial já foi criado." });
        }

        if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length < 12)
        {
            return BadRequest(new { message = "A senha deve ter pelo menos 12 caracteres." });
        }

        string cpf;
        try
        {
            cpf = CpfValidator.NormalizeAndValidate(request.Cpf);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        if (await db.Pessoas.AnyAsync(pessoa => pessoa.Cpf == cpf, cancellationToken))
        {
            return Conflict(new { message = "Este CPF já está cadastrado." });
        }

        var person = new Pessoa { Cpf = cpf, NomeCompleto = request.Nome.Trim(), Email = request.Email.Trim() };
        var user = new Usuario { Pessoa = person, SuperAdmin = true };
        user.SenhaHash = passwordHasher.HashPassword(user, request.Senha);
        db.Usuarios.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Created("/api/auth/login", new { message = "Superadmin criado. Agora entre pelo endpoint de login." });
    }

    [AllowAnonymous]
    [HttpPost("registro")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Categoria)) return BadRequest(new { message = "Selecione uma categoria válida." });
        if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length < 12)
        {
            return BadRequest(new { message = "A senha deve ter pelo menos 12 caracteres." });
        }

        if (!await db.Clubes.AnyAsync(clube => clube.Id == request.ClubeId && clube.Ativo, cancellationToken))
        {
            return NotFound(new { message = "Clube não encontrado ou inativo." });
        }

        string cpf;
        try
        {
            cpf = CpfValidator.NormalizeAndValidate(request.Cpf);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        if (await db.Pessoas.AnyAsync(pessoa => pessoa.Cpf == cpf, cancellationToken))
        {
            return Conflict(new { message = "Este CPF já está cadastrado. Entre na conta existente para solicitar outro vínculo." });
        }

        if (!await localidades.IsValidMunicipioAsync(request.Uf, request.Cidade, cancellationToken))
            return BadRequest(new { message = "Selecione um estado e município válidos nas listas oficiais." });

        var person = new Pessoa
        {
            Cpf = cpf,
            NomeCompleto = request.Nome.Trim(),
            Email = request.Email.Trim(),
            Telefone = request.Telefone.Trim(),
            Endereco = request.Endereco.Trim(),
            Bairro = request.Bairro.Trim(),
            Cidade = request.Cidade.Trim(),
            Uf = request.Uf.Trim().ToUpperInvariant()
        };
        var user = new Usuario { Pessoa = person };
        user.SenhaHash = passwordHasher.HashPassword(user, request.Senha);
        db.Usuarios.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var requestRecord = new SolicitacaoIngresso
        {
            PessoaId = person.Id,
            ClubeId = request.ClubeId,
            Categoria = request.Categoria,
            Justificativa = request.Justificativa.Trim()
        };
        db.SolicitacoesIngresso.Add(requestRecord);
        await db.SaveChangesAsync(cancellationToken);

        return Accepted(new { requestRecord.Id, status = requestRecord.Situacao.ToString(), message = "Pedido enviado e aguardando aprovação." });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        string cpf;
        try
        {
            cpf = CpfValidator.NormalizeAndValidate(request.Cpf);
        }
        catch (ArgumentException)
        {
            return Unauthorized(new { message = "CPF ou senha inválidos." });
        }

        var user = await db.Usuarios
            .Include(usuario => usuario.Pessoa)
            .SingleOrDefaultAsync(usuario => usuario.Pessoa.Cpf == cpf && usuario.Ativo, cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.SenhaHash, request.Senha) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "CPF ou senha inválidos." });
        }

        var clubIds = user.SuperAdmin
            ? await db.Clubes.Where(clube => clube.Ativo).Select(clube => clube.Id).ToListAsync(cancellationToken)
            : await GetAllowedClubIdsAsync(user, cancellationToken);
        if (clubIds.Count == 0 && !user.SuperAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Sua conta ainda não possui acesso aprovado a um clube." });
        }

        int? selectedClubId = request.ClubeId ?? (clubIds.Count > 0 ? clubIds[0] : null);
        if (selectedClubId is int selected && !clubIds.Contains(selected))
        {
            return Forbid();
        }

        var profile = selectedClubId is int activeClubId ? await ResolveProfileAsync(user, activeClubId, cancellationToken) : "SuperAdmin";
        var session = await CreateSessionAsync(user, selectedClubId, profile, cancellationToken);
        return Ok(ToLoginResponse(session, user, clubIds));
    }

    [Authorize]
    [HttpPost("selecionar-clube")]
    public async Task<IActionResult> SelectClub(SelectClubRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Usuarios.Include(usuario => usuario.Pessoa).SingleAsync(usuario => usuario.Id == UserId, cancellationToken);
        var allowedClubIds = user.SuperAdmin
            ? await db.Clubes.Where(clube => clube.Ativo).Select(clube => clube.Id).ToListAsync(cancellationToken)
            : await GetAllowedClubIdsAsync(user, cancellationToken);
        if (!allowedClubIds.Contains(request.ClubeId))
        {
            return Forbid();
        }

        if (Guid.TryParse(User.FindFirst("session_id")?.Value, out var oldSessionId))
        {
            var oldSession = await db.Sessoes.FindAsync([oldSessionId], cancellationToken);
            if (oldSession is not null)
            {
                oldSession.RevogadaEmUtc = DateTime.UtcNow;
            }
        }

        var profile = await ResolveProfileAsync(user, request.ClubeId, cancellationToken);
        var newSession = await CreateSessionAsync(user, request.ClubeId, profile, cancellationToken);
        return Ok(ToLoginResponse(newSession, user, allowedClubIds));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await db.Usuarios.Include(usuario => usuario.Pessoa).SingleAsync(usuario => usuario.Id == UserId, cancellationToken);
        List<int> allowedClubIds;
        if (user.SuperAdmin)
        {
            allowedClubIds = await db.Clubes.Where(clube => clube.Ativo).Select(clube => clube.Id).ToListAsync(cancellationToken);
        }
        else
        {
            allowedClubIds = await GetAllowedClubIdsAsync(user, cancellationToken);
        }

        var clubs = await db.Clubes
            .Where(clube => allowedClubIds.Contains(clube.Id) && clube.Ativo)
            .Select(clube => new { clube.Id, clube.Nome, clube.Sigla })
            .ToListAsync(cancellationToken);

        return Ok(new { user.Pessoa.NomeCompleto, user.Pessoa.Cpf, perfil = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, clubeId = SelectedClubId, clubes = clubs });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(User.FindFirst("session_id")?.Value, out var sessionId))
        {
            var session = await db.Sessoes.FindAsync([sessionId], cancellationToken);
            if (session is not null)
            {
                session.RevogadaEmUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return NoContent();
    }

    private async Task<List<int>> GetAllowedClubIdsAsync(Usuario user, CancellationToken cancellationToken)
    {
        var membershipClubIds = await db.Associacoes
            .Where(association => association.PessoaId == user.PessoaId && association.Situacao == SituacaoAssociacao.Ativa && association.Clube.Ativo)
            .Select(association => association.ClubeId)
            .ToListAsync(cancellationToken);
        var roleClubIds = await db.VinculosUsuario.Where(link => link.UsuarioId == user.Id && link.Clube.Ativo).Select(link => link.ClubeId).ToListAsync(cancellationToken);
        return membershipClubIds.Concat(roleClubIds).Distinct().ToList();
    }

    private async Task<string> ResolveProfileAsync(Usuario user, int clubId, CancellationToken cancellationToken)
    {
        if (user.SuperAdmin)
        {
            return "SuperAdmin";
        }

        var role = await db.VinculosUsuario.Where(link => link.UsuarioId == user.Id && link.ClubeId == clubId)
            .Select(link => (PerfilClube?)link.Perfil)
            .SingleOrDefaultAsync(cancellationToken);
        if (role is not null)
        {
            return role.Value.ToString();
        }

        return "Associado";
    }

    private async Task<(SessaoUsuario Session, string Token)> CreateSessionAsync(Usuario user, int? clubId, string profile, CancellationToken cancellationToken)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var session = new SessaoUsuario
        {
            UsuarioId = user.Id,
            ClubeId = clubId,
            Perfil = profile,
            TokenHash = TokenDigest.Hash(token),
            ExpiraEmUtc = DateTime.UtcNow.AddMinutes(30)
        };
        db.Sessoes.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return (session, token);
    }

    private static object ToLoginResponse((SessaoUsuario Session, string Token) result, Usuario user, List<int> clubIds) => new
    {
        accessToken = result.Token,
        tokenType = "Bearer",
        expiresAtUtc = result.Session.ExpiraEmUtc,
        userId = user.Id,
        personId = user.PessoaId,
        name = user.Pessoa.NomeCompleto,
        role = result.Session.Perfil,
        activeClubId = result.Session.ClubeId,
        availableClubIds = clubIds
    };

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(provided ?? string.Empty);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public sealed record BootstrapRequest([param: Required] string SetupKey, [param: Required] string Cpf, [param: Required, StringLength(160, MinimumLength = 2)] string Nome, [param: Required, EmailAddress, StringLength(200)] string Email, [param: Required, MinLength(12)] string Senha);
    public sealed record RegisterRequest([param: Required] string Cpf, [param: Required, StringLength(160, MinimumLength = 2)] string Nome, [param: Required, EmailAddress, StringLength(200)] string Email, [param: StringLength(30)] string Telefone, [param: Required, MinLength(12)] string Senha, [param: Range(1, int.MaxValue)] int ClubeId, CategoriaSolicitada Categoria, [param: Required, StringLength(500, MinimumLength = 5)] string Justificativa, [param: StringLength(200)] string Endereco = "", [param: StringLength(100)] string Bairro = "", [param: Required] string Cidade = "", [param: Required, StringLength(2, MinimumLength = 2)] string Uf = "");
    public sealed record LoginRequest([param: Required] string Cpf, [param: Required] string Senha, int? ClubeId);
    public sealed record SelectClubRequest([param: Range(1, int.MaxValue)] int ClubeId);
}
