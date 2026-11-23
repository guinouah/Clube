using System.ComponentModel.DataAnnotations;
using Clubes.Api.Data;
using Clubes.Api.Models;
using Clubes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clubes.Api.Controllers;

[Route("api/clubes")]
public sealed class ClubesController(
    ClubesDbContext db,
    IPasswordHasher<Usuario> passwordHasher,
    LocalidadesService localidades) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var clubs = await db.Clubes
            .Where(clube => clube.Ativo)
            .OrderBy(clube => clube.Nome)
            .Select(clube => new { clube.Id, clube.Nome, clube.Sigla, clube.Cidade, clube.Uf })
            .ToListAsync(cancellationToken);
        return Ok(clubs);
    }

    [Authorize]
    [HttpGet("{clubeId:int}")]
    public async Task<IActionResult> Get(int clubeId, CancellationToken cancellationToken)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var club = await db.Clubes.Where(clube => clube.Id == clubeId && clube.Ativo)
            .Select(clube => new { clube.Id, clube.Nome, clube.Sigla, clube.Endereco, clube.Bairro, clube.Cidade, clube.Uf, clube.Telefone, clube.Email })
            .SingleOrDefaultAsync(cancellationToken);
        return club is null ? NotFound() : Ok(club);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(ClubeRequest request, CancellationToken cancellationToken)
    {
        if (!await localidades.IsValidMunicipioAsync(request.Uf, request.Cidade, cancellationToken))
            return BadRequest(new { message = "Selecione um estado e município válidos nas listas oficiais." });
        var sigla = request.Sigla.Trim().ToUpperInvariant();
        if (await db.Clubes.AnyAsync(clube => clube.Sigla == sigla, cancellationToken))
            return Conflict(new { message = "A sigla já está cadastrada." });

        var club = new Clube
        {
            Nome = request.Nome.Trim(),
            Sigla = sigla,
            Endereco = request.Endereco.Trim(),
            Bairro = request.Bairro.Trim(),
            Cidade = request.Cidade.Trim(),
            Uf = request.Uf.Trim().ToUpperInvariant(),
            Telefone = request.Telefone.Trim(),
            Email = request.Email.Trim()
        };
        db.Clubes.Add(club);
        await db.SaveChangesAsync(cancellationToken);
        db.DiasVencimento.AddRange(new[] { 5, 10, 15, 20 }.Select(dia => new DiaVencimentoClube { ClubeId = club.Id, Dia = dia }));
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { clubeId = club.Id }, new { club.Id, club.Nome, club.Sigla });
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{clubeId:int}")]
    public async Task<IActionResult> Update(int clubeId, ClubeRequest request, CancellationToken cancellationToken)
    {
        if (!await localidades.IsValidMunicipioAsync(request.Uf, request.Cidade, cancellationToken))
            return BadRequest(new { message = "Selecione um estado e município válidos nas listas oficiais." });
        var club = await db.Clubes.FindAsync([clubeId], cancellationToken);
        if (club is null) return NotFound();

        var sigla = request.Sigla.Trim().ToUpperInvariant();
        if (await db.Clubes.AnyAsync(item => item.Id != clubeId && item.Sigla == sigla, cancellationToken))
            return Conflict(new { message = "A sigla já está cadastrada." });

        club.Nome = request.Nome.Trim();
        club.Sigla = sigla;
        club.Endereco = request.Endereco.Trim();
        club.Bairro = request.Bairro.Trim();
        club.Cidade = request.Cidade.Trim();
        club.Uf = request.Uf.Trim().ToUpperInvariant();
        club.Telefone = request.Telefone.Trim();
        club.Email = request.Email.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SuperAdmin,Presidente")]
    [HttpGet("{clubeId:int}/associados")]
    public async Task<IActionResult> Members(int clubeId, CancellationToken cancellationToken)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var memberData = await db.Associacoes
            .Where(association => association.ClubeId == clubeId && association.Situacao == SituacaoAssociacao.Ativa)
            .OrderBy(association => association.Pessoa.NomeCompleto)
            .Select(association => new
            {
                association.PessoaId,
                association.Pessoa.NomeCompleto,
                association.Pessoa.Cpf,
                association.Pessoa.Email,
                categoria = association.Categoria.ToString(),
                association.IngressouEmUtc
            })
            .ToListAsync(cancellationToken);
        var members = memberData.Select(member => new { member.PessoaId, member.NomeCompleto, Cpf = MaskCpf(member.Cpf), member.Email, member.categoria, member.IngressouEmUtc });
        return Ok(members);
    }

    [Authorize(Roles = "SuperAdmin,Presidente")]
    [HttpPost("{clubeId:int}/usuarios")]
    public async Task<IActionResult> AssignUser(int clubeId, AssignUserRequest request, CancellationToken cancellationToken)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (!Enum.IsDefined(request.Perfil)) return BadRequest(new { message = "Perfil inválido." });
        if (request.Perfil == PerfilClube.Presidente && !IsSuperAdmin) return Forbid();

        var club = await db.Clubes.SingleOrDefaultAsync(item => item.Id == clubeId && item.Ativo, cancellationToken);
        if (club is null) return NotFound();

        string cpf;
        try { cpf = CpfValidator.NormalizeAndValidate(request.Cpf); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }

        var person = await db.Pessoas.SingleOrDefaultAsync(item => item.Cpf == cpf, cancellationToken);
        if (person is null)
        {
            person = new Pessoa { Cpf = cpf, NomeCompleto = request.Nome.Trim(), Email = request.Email.Trim() };
            db.Pessoas.Add(person);
            await db.SaveChangesAsync(cancellationToken);
        }

        var user = await db.Usuarios.SingleOrDefaultAsync(item => item.PessoaId == person.Id, cancellationToken);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length < 12)
                return BadRequest(new { message = "Para criar um acesso, informe uma senha com pelo menos 12 caracteres." });
            user = new Usuario { PessoaId = person.Id };
            user.SenhaHash = passwordHasher.HashPassword(user, request.Senha);
            db.Usuarios.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (request.Perfil == PerfilClube.Associado && await db.Associacoes.AnyAsync(row => row.PessoaId == person.Id && row.ClubeId != clubeId, cancellationToken))
            return Conflict(new { message = "Esta pessoa já está associada a outro clube." });

        if (request.Perfil == PerfilClube.Associado && !await db.Associacoes.AnyAsync(row => row.PessoaId == person.Id && row.ClubeId == clubeId, cancellationToken))
            db.Associacoes.Add(new AssociacaoClube { PessoaId = person.Id, ClubeId = clubeId, Categoria = CategoriaAssociado.FundadorAssociado });

        var link = await db.VinculosUsuario.FindAsync([user.Id, clubeId], cancellationToken);
        if (link is null)
        {
            db.VinculosUsuario.Add(new VinculoUsuarioClube { UsuarioId = user.Id, ClubeId = clubeId, Perfil = request.Perfil });
        }
        else
        {
            link.Perfil = request.Perfil;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { user.Id, person.NomeCompleto, perfil = request.Perfil.ToString(), clubeId });
    }

    [Authorize(Roles = "SuperAdmin,Presidente")]
    [HttpPatch("{clubeId:int}/desativar")]
    public async Task<IActionResult> Deactivate(int clubeId, CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin) return Forbid();
        var club = await db.Clubes.FindAsync([clubeId], cancellationToken);
        if (club is null) return NotFound();
        club.Ativo = false;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string MaskCpf(string cpf) => $"***.***.{cpf[6..9]}-{cpf[9..]}";

    public sealed record ClubeRequest(
        [param: Required, StringLength(160, MinimumLength = 2)] string Nome,
        [param: Required, StringLength(20, MinimumLength = 2)] string Sigla,
        [param: StringLength(200)] string Endereco,
        [param: StringLength(100)] string Bairro,
        [param: Required, StringLength(100)] string Cidade,
        [param: Required, StringLength(2, MinimumLength = 2)] string Uf,
        [param: StringLength(30)] string Telefone,
        [param: EmailAddress, StringLength(200)] string Email);

    public sealed record AssignUserRequest([param: Required] string Cpf, [param: Required, StringLength(160, MinimumLength = 2)] string Nome, [param: EmailAddress, StringLength(200)] string Email, string Senha, PerfilClube Perfil);
}
