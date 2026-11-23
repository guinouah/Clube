using System.Security.Claims;
using System.Text.Encodings.Web;
using Clubes.Api.Data;
using Clubes.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Clubes.Api.Security;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ClubesDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = Request.Headers.Authorization.ToString()[7..].Trim();
        if (token.Length < 40)
        {
            return AuthenticateResult.Fail("Token inválido.");
        }

        var digest = TokenDigest.Hash(token);
        var session = await db.Sessoes
            .Include(item => item.Usuario)
            .ThenInclude(usuario => usuario.Pessoa)
            .SingleOrDefaultAsync(item => item.TokenHash == digest, Context.RequestAborted);

        if (session is null || session.RevogadaEmUtc is not null || session.ExpiraEmUtc <= DateTime.UtcNow || !session.Usuario.Ativo)
        {
            return AuthenticateResult.Fail("Sessão expirada ou revogada. Entre novamente.");
        }

        if (!session.Usuario.SuperAdmin && session.ClubeId is int clubId)
        {
            var isMember = await db.Associacoes.AnyAsync(
                association => association.PessoaId == session.Usuario.PessoaId && association.ClubeId == clubId && association.Situacao == Models.SituacaoAssociacao.Ativa && association.Clube.Ativo,
                Context.RequestAborted);
            var hasRole = await db.VinculosUsuario.AnyAsync(
                link => link.UsuarioId == session.UsuarioId && link.ClubeId == clubId && link.Clube.Ativo,
                Context.RequestAborted);
            if (!isMember && !hasRole)
            {
                return AuthenticateResult.Fail("O acesso a este clube foi removido.");
            }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UsuarioId.ToString()),
            new(ClaimTypes.Name, session.Usuario.Pessoa.NomeCompleto),
            new("person_id", session.Usuario.PessoaId.ToString()),
            new("session_id", session.Id.ToString()),
            new(ClaimTypes.Role, session.Perfil)
        };

        if (session.ClubeId is int selectedClubId)
        {
            claims.Add(new Claim("club_id", selectedClubId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
