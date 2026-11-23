using System.ComponentModel.DataAnnotations;
using Clubes.Api.Data;
using Clubes.Api.Models;
using Clubes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clubes.Api.Controllers;

[Route("api/clubes/{clubeId:int}")]
[Authorize]
public sealed class OperacoesController(ClubesDbContext db, ClubMembershipService associations) : ApiControllerBase
{
    private static readonly SemaphoreSlim[] AgendaLocks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    [HttpGet("eventos")]
    public async Task<IActionResult> Eventos(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var query = db.Eventos.Where(item => item.ClubeId == clubeId && item.Ativo);
        if (!IsPresident && !IsSuperAdmin) query = query.Where(item => item.PublicoGeral || User.IsInRole("Associado"));
        return Ok(await query.OrderBy(item => item.InicioUtc).Select(item => new { item.Id, item.Nome, item.Descricao, item.InicioUtc, item.FimUtc, item.Local, item.EspacoId, item.PublicoGeral, item.Beneficente }).ToListAsync(ct));
    }

    [HttpPost("eventos")]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public async Task<IActionResult> CriarEvento(int clubeId, EventoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (request.FimUtc <= request.InicioUtc) return BadRequest(new { message = "O término deve ser posterior ao início." });
        var spaceLock = $"club:{clubeId}";
        return await WithAgendaLocksAsync<IActionResult>([spaceLock], async () =>
        {
            if (request.EspacoId is int espacoId && !await db.Espacos.AnyAsync(space => space.Id == espacoId && space.ClubeId == clubeId && space.Ativo, ct))
                return BadRequest(new { message = "O espaço selecionado não pertence a este clube ou está inativo." });
            if (request.EspacoId is int selectedSpace && await HasScheduleConflict(clubeId, selectedSpace, request.InicioUtc, request.FimUtc, ct))
                return Conflict(new { message = "Já existe uma reserva ou evento nesse espaço e horário." });
            var item = new EventoClube { ClubeId = clubeId, EspacoId = request.EspacoId, Nome = request.Nome.Trim(), Descricao = request.Descricao?.Trim() ?? "", Local = request.Local?.Trim() ?? "", InicioUtc = request.InicioUtc, FimUtc = request.FimUtc, PublicoGeral = request.PublicoGeral, Beneficente = request.Beneficente };
            db.Eventos.Add(item); await db.SaveChangesAsync(ct);
            return Created($"/api/clubes/{clubeId}/eventos/{item.Id}", new { item.Id });
        }, ct);
    }

    [HttpGet("espacos")]
    public async Task<IActionResult> Espacos(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.Espacos.Where(item => item.ClubeId == clubeId && item.Ativo).OrderBy(item => item.Nome).Select(item => new { item.Id, item.Nome, item.Tipo, item.Descricao, item.Capacidade, abreAs = item.AbreAs.ToString("HH:mm"), fechaAs = item.FechaAs.ToString("HH:mm"), item.PermiteDependentes }).ToListAsync(ct));
    }

    [HttpPost("espacos")]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public async Task<IActionResult> CriarEspaco(int clubeId, EspacoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var nome = request.Nome.Trim();
        if (await db.Espacos.AnyAsync(item => item.ClubeId == clubeId && item.Nome == nome, ct)) return Conflict(new { message = "Já existe um espaço com esse nome." });
        if (request.Capacidade is <= 0) return BadRequest(new { message = "A capacidade deve ser maior que zero." });
        var open = request.AbreAs ?? new TimeOnly(8, 0);
        var close = request.FechaAs ?? new TimeOnly(22, 0);
        if (close <= open) return BadRequest(new { message = "O fechamento deve ser depois da abertura." });
        var item = new EspacoClube { ClubeId = clubeId, Nome = nome, Tipo = request.Tipo.Trim(), Descricao = request.Descricao?.Trim() ?? "", Capacidade = request.Capacidade, AbreAs = open, FechaAs = close, PermiteDependentes = request.PermiteDependentes };
        db.Espacos.Add(item); await db.SaveChangesAsync(ct);
        return Created($"/api/clubes/{clubeId}/espacos/{item.Id}", new { item.Id });
    }

    [HttpGet("reservas")]
    public async Task<IActionResult> Reservas(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var query = db.Reservas.Include(item => item.Espaco).Include(item => item.Participantes).Where(item => item.ClubeId == clubeId);
        if (!IsPresident && !IsSuperAdmin) query = query.Where(item => item.PessoaId == PersonId);
        return Ok(await query.OrderByDescending(item => item.CriadoEmUtc).Select(item => new { item.Id, item.EspacoId, espaco = item.Espaco.Nome, tipoEspaco = item.Espaco.Tipo, item.PessoaId, item.DependenteId, item.Finalidade, item.InicioUtc, item.FimUtc, participantes = item.Participantes.Select(p => new { p.PessoaId, p.DependenteId, p.NomeConvidado }), situacao = item.Situacao.ToString(), item.ObservacaoAvaliacao }).ToListAsync(ct));
    }

    [HttpPost("reservas")]
    public async Task<IActionResult> CriarReserva(int clubeId, ReservaRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (!IsSuperAdmin && !await IsActiveMemberAsync(clubeId, ct)) return Forbid();
        if (request.IdempotencyKey == Guid.Empty) return BadRequest(new { message = "Informe uma chave única para evitar pedidos duplicados durante a sincronização." });
        if (request.FimUtc <= request.InicioUtc || request.InicioUtc <= DateTime.UtcNow) return BadRequest(new { message = "O horário da reserva deve ser futuro e o término posterior ao início." });
        var duration = request.FimUtc - request.InicioUtc;
        if (duration is not { TotalMinutes: 30 or 60 or 90 }) return BadRequest(new { message = "Escolha uma duração de 30, 60 ou 90 minutos." });
        var keys = new[] { $"club:{clubeId}", $"idempotency:{request.IdempotencyKey:N}" };
        return await WithAgendaLocksAsync<IActionResult>(keys, async () =>
        {
            var existing = await db.Reservas.SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is not null) return Ok(new { existing.Id, situacao = existing.Situacao.ToString(), duplicado = true });
            var space = await db.Espacos.SingleOrDefaultAsync(item => item.Id == request.EspacoId && item.ClubeId == clubeId && item.Ativo, ct);
            if (space is null) return BadRequest(new { message = "Espaço não encontrado ou indisponível." });
            var startTime = TimeOnly.FromDateTime(ToBrazilianLocal(request.InicioUtc));
            var endTime = TimeOnly.FromDateTime(ToBrazilianLocal(request.FimUtc));
            if (startTime < space.AbreAs || endTime > space.FechaAs) return BadRequest(new { message = $"O espaço funciona das {space.AbreAs:HH:mm} às {space.FechaAs:HH:mm}." });
            if (request.DependenteId is int dependentId && (!space.PermiteDependentes || !await db.Dependentes.AnyAsync(d => d.Id == dependentId && d.TitularPessoaId == PersonId && d.Ativo, ct)))
                return Forbid();
            var participantIds = request.Participantes ?? [];
            var validParticipantCount = await db.Associacoes.CountAsync(a => participantIds.Contains(a.PessoaId) && a.ClubeId == clubeId && a.Situacao == SituacaoAssociacao.Ativa, ct);
            if (validParticipantCount != participantIds.Distinct().Count()) return BadRequest(new { message = "Um ou mais participantes não são associados ativos deste clube." });
            var participantRows = participantIds.Distinct().Where(id => id != PersonId).Select(id => new ReservaParticipante { PessoaId = id });
            var guestNames = (request.Convidados ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (space.Capacidade is int capacity && 1 + participantIds.Distinct().Count() + (request.DependenteId.HasValue ? 1 : 0) + guestNames.Count > capacity)
                return BadRequest(new { message = $"A capacidade máxima deste espaço é {capacity} pessoas." });
            if (await HasScheduleConflict(clubeId, space.Id, request.InicioUtc, request.FimUtc, ct)) return Conflict(new { message = "Já existe um evento ou reserva nesse horário." });
            var item = new Reserva { IdempotencyKey = request.IdempotencyKey, ClubeId = clubeId, EspacoId = space.Id, PessoaId = PersonId, DependenteId = request.DependenteId, Finalidade = request.Finalidade.Trim(), InicioUtc = request.InicioUtc, FimUtc = request.FimUtc, Participantes = participantRows.ToList() };
            foreach (var guest in guestNames) item.Participantes.Add(new ReservaParticipante { NomeConvidado = guest });
            db.Reservas.Add(item); await db.SaveChangesAsync(ct);
            return Accepted(new { item.Id, situacao = item.Situacao.ToString(), message = "Pedido recebido e aguardando avaliação; ainda não está confirmado." });
        }, ct);
    }

    [HttpPatch("reservas/{reservaId:guid}/avaliacao")]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public async Task<IActionResult> AvaliarReserva(int clubeId, Guid reservaId, AvaliacaoReservaRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var current = await db.Reservas.AsNoTracking().SingleOrDefaultAsync(reserva => reserva.Id == reservaId && reserva.ClubeId == clubeId, ct);
        if (current is null) return NotFound();
        return await WithAgendaLocksAsync<IActionResult>([$"club:{clubeId}"], async () =>
        {
            var item = await db.Reservas.SingleOrDefaultAsync(reserva => reserva.Id == reservaId && reserva.ClubeId == clubeId, ct);
            if (item is null) return NotFound();
            if (item.Situacao != SituacaoReserva.Pendente) return Conflict(new { message = "Somente pedidos pendentes podem ser avaliados." });
            if (request.Aprovar && await HasScheduleConflict(clubeId, item.EspacoId, item.InicioUtc, item.FimUtc, ct, item.Id)) return Conflict(new { message = "O horário deixou de estar disponível; o pedido não foi aprovado." });
            item.Situacao = request.Aprovar ? SituacaoReserva.Aprovada : SituacaoReserva.Recusada;
            item.AvaliadoPorUsuarioId = UserId; item.ObservacaoAvaliacao = request.Observacao?.Trim();
            await db.SaveChangesAsync(ct); return NoContent();
        }, ct);
    }

    [HttpGet("solicitacoes")]
    public async Task<IActionResult> Manifestacoes(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var query = db.Manifestacoes.Where(item => item.ClubeId == clubeId);
        if (!IsPresident && !IsSuperAdmin) query = query.Where(item => item.AutorPessoaId == PersonId);
        return Ok(await query.OrderByDescending(item => item.CriadaEmUtc).Select(item => new { item.Id, tipo = item.Tipo.ToString(), item.Assunto, item.Descricao, situacao = item.Situacao.ToString(), item.CriadaEmUtc }).ToListAsync(ct));
    }

    [HttpPost("solicitacoes")]
    public async Task<IActionResult> CriarManifestacao(int clubeId, ManifestacaoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (!IsSuperAdmin && !await IsActiveMemberAsync(clubeId, ct)) return Forbid();
        if (!Enum.IsDefined(request.Tipo)) return BadRequest(new { message = "Tipo de manifestação inválido." });
        var item = new Manifestacao { ClubeId = clubeId, AutorPessoaId = PersonId, Tipo = request.Tipo, Assunto = request.Assunto.Trim(), Descricao = request.Descricao.Trim() };
        db.Manifestacoes.Add(item); await db.SaveChangesAsync(ct);
        return Accepted(new { item.Id, situacao = item.Situacao.ToString() });
    }

    [HttpPatch("solicitacoes/{id:guid}/situacao")]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public async Task<IActionResult> AtualizarManifestacao(int clubeId, Guid id, AtualizarManifestacaoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var item = await db.Manifestacoes.SingleOrDefaultAsync(row => row.Id == id && row.ClubeId == clubeId, ct);
        if (item is null) return NotFound();
        if (!Enum.IsDefined(request.Situacao)) return BadRequest(new { message = "Situação inválida." });
        item.Situacao = request.Situacao; item.ResponsavelUsuarioId = UserId; await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("mensalidades/minhas")]
    public async Task<IActionResult> MinhasMensalidades(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.Mensalidades.Where(item => item.ClubeId == clubeId && item.PessoaId == PersonId).OrderByDescending(item => item.Ano).ThenByDescending(item => item.Mes).Select(item => new { item.Id, item.Ano, item.Mes, item.Vencimento, item.Valor, situacao = item.Situacao.ToString(), item.PagaEmUtc }).ToListAsync(ct));
    }

    [HttpGet("interesses")]
    public async Task<IActionResult> Interesses(CancellationToken ct) => Ok(await db.Interesses.OrderBy(i => i.Nome).Select(i => new { i.Id, i.Nome }).ToListAsync(ct));

    [HttpGet("interesses/meus")]
    public async Task<IActionResult> MeusInteresses(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.PessoasInteresses.Where(row => row.PessoaId == PersonId).OrderBy(row => row.Interesse.Nome).Select(row => new { row.InteresseId, row.Interesse.Nome }).ToListAsync(ct));
    }

    [HttpPut("interesses/meus")]
    public async Task<IActionResult> AtualizarInteresses(int clubeId, InteressesRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (!await IsActiveMemberAsync(clubeId, ct)) return Forbid();
        var names = (request.Nomes ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();
        var normalized = names.Select(name => name.ToUpperInvariant()).ToHashSet();
        var catalog = await db.Interesses.ToListAsync(ct);
        foreach (var name in names.Where(name => catalog.All(i => !string.Equals(i.Nome, name, StringComparison.OrdinalIgnoreCase))))
        {
            var interest = new Interesse { Nome = name };
            db.Interesses.Add(interest);
            catalog.Add(interest);
        }
        await db.SaveChangesAsync(ct);
        var selectedIds = catalog.Where(i => normalized.Contains(i.Nome.ToUpperInvariant())).Select(i => i.Id).ToHashSet();
        var current = await db.PessoasInteresses.Where(row => row.PessoaId == PersonId).ToListAsync(ct);
        db.PessoasInteresses.RemoveRange(current.Where(row => !selectedIds.Contains(row.InteresseId)));
        var currentIds = current.Select(row => row.InteresseId).ToHashSet();
        db.PessoasInteresses.AddRange(selectedIds.Where(id => !currentIds.Contains(id)).Select(id => new PessoaInteresse { PessoaId = PersonId, InteresseId = id }));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("mensalidades/dias-vencimento")]
    public async Task<IActionResult> DiasVencimento(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.DiasVencimento.Where(row => row.ClubeId == clubeId).OrderBy(row => row.Dia).Select(row => row.Dia).ToListAsync(ct));
    }

    [HttpPut("mensalidades/dias-vencimento")]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public async Task<IActionResult> ConfigurarDiasVencimento(int clubeId, DiasVencimentoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var days = (request.Dias ?? []).Distinct().Order().ToList();
        if (days.Count == 0 || days.Any(day => day is < 1 or > 28)) return BadRequest(new { message = "Informe um ou mais dias entre 1 e 28." });
        var current = await db.DiasVencimento.Where(row => row.ClubeId == clubeId).ToListAsync(ct);
        db.DiasVencimento.RemoveRange(current);
        db.DiasVencimento.AddRange(days.Select(day => new DiaVencimentoClube { ClubeId = clubeId, Dia = day }));
        await db.SaveChangesAsync(ct);
        return Ok(days);
    }

    [HttpPut("mensalidades/minha-data-vencimento")]
    public async Task<IActionResult> EscolherDiaVencimento(int clubeId, EscolherDiaVencimentoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var association = await db.Associacoes.SingleOrDefaultAsync(row => row.PessoaId == PersonId && row.ClubeId == clubeId && row.Situacao == SituacaoAssociacao.Ativa, ct);
        if (association is null) return Forbid();
        if (!await db.DiasVencimento.AnyAsync(row => row.ClubeId == clubeId && row.Dia == request.Dia, ct)) return BadRequest(new { message = "Esse dia não está disponível para este clube." });
        association.DiaVencimento = request.Dia;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("mensalidades/minha-data-vencimento")]
    public async Task<IActionResult> MinhaDataVencimento(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var association = await db.Associacoes.SingleOrDefaultAsync(row => row.PessoaId == PersonId && row.ClubeId == clubeId, ct);
        if (association is null) return Forbid();
        var options = await db.DiasVencimento.Where(row => row.ClubeId == clubeId).OrderBy(row => row.Dia).Select(row => row.Dia).ToListAsync(ct);
        return Ok(new { association.DiaVencimento, opcoes = options });
    }

    [HttpGet("mensalidades")]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public async Task<IActionResult> TodasMensalidades(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.Mensalidades.Include(item => item.Pessoa).Where(item => item.ClubeId == clubeId).OrderByDescending(item => item.Ano).ThenByDescending(item => item.Mes).Select(item => new { item.Id, item.PessoaId, nome = item.Pessoa.NomeCompleto, cpf = item.Pessoa.Cpf, item.Ano, item.Mes, item.Vencimento, item.Valor, situacao = item.Situacao.ToString() }).ToListAsync(ct));
    }

    [HttpPut("mensalidades/valores")]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public async Task<IActionResult> DefinirValor(int clubeId, ValorMensalidadeRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (!Enum.IsDefined(request.Categoria)) return BadRequest(new { message = "Categoria inválida." });
        if (request.Valor <= 0) return BadRequest(new { message = "O valor deve ser maior que zero." });
        var item = await db.ValoresMensalidade.SingleOrDefaultAsync(row => row.ClubeId == clubeId && row.Categoria == request.Categoria && row.VigenteDesde == request.VigenteDesde, ct);
        if (item is null) db.ValoresMensalidade.Add(new ValorMensalidade { ClubeId = clubeId, Categoria = request.Categoria, Valor = request.Valor, VigenteDesde = request.VigenteDesde });
        else { item.Valor = request.Valor; item.Ativo = true; }
        await db.SaveChangesAsync(ct); return Ok(new { request.Categoria, request.Valor, request.VigenteDesde });
    }

    [HttpPost("mensalidades/gerar")]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public async Task<IActionResult> GerarMensalidades(int clubeId, GerarMensalidadesRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (request.Mes is < 1 or > 12 || request.Ano is < 2020 or > 2100) return BadRequest(new { message = "Informe uma competência válida." });
        if (request.Vencimento is DateOnly dueDate && (dueDate.Year != request.Ano || dueDate.Month != request.Mes)) return BadRequest(new { message = "A data de vencimento precisa pertencer à competência informada." });
        var members = await db.Associacoes.Where(row => row.ClubeId == clubeId && row.Situacao == SituacaoAssociacao.Ativa).Select(row => new { row.PessoaId, row.Categoria, row.DiaVencimento }).ToListAsync(ct);
        var fee = await db.ValoresMensalidade.Where(row => row.ClubeId == clubeId && row.Ativo && row.VigenteDesde <= new DateOnly(request.Ano, request.Mes, 1)).OrderByDescending(row => row.VigenteDesde).ToListAsync(ct);
        var availableDays = await db.DiasVencimento.Where(row => row.ClubeId == clubeId).Select(row => row.Dia).ToListAsync(ct);
        var existing = await db.Mensalidades.Where(row => row.ClubeId == clubeId && row.Ano == request.Ano && row.Mes == request.Mes).Select(row => row.PessoaId).ToListAsync(ct);
        var issued = 0;
        foreach (var member in members.Where(row => !existing.Contains(row.PessoaId)))
        {
            var amount = fee.FirstOrDefault(row => row.Categoria == member.Categoria)?.Valor;
            if (amount is null) continue;
            var configuredDay = availableDays.Contains(member.DiaVencimento) ? member.DiaVencimento : availableDays.FirstOrDefault(10);
            var lastDay = DateTime.DaysInMonth(request.Ano, request.Mes);
            var vencimento = request.Vencimento ?? new DateOnly(request.Ano, request.Mes, Math.Min(configuredDay, lastDay));
            db.Mensalidades.Add(new Mensalidade { ClubeId = clubeId, PessoaId = member.PessoaId, Ano = request.Ano, Mes = request.Mes, Valor = amount.Value, Vencimento = vencimento }); issued++;
        }
        await db.SaveChangesAsync(ct); return Ok(new { criadas = issued, ignoradas = members.Count - issued, message = "Mensalidades criadas para os associados com valor configurado." });
    }

    [HttpPatch("mensalidades/{id:guid}/pagamento")]
    [Authorize(Roles = "SuperAdmin,Tesoureiro")]
    public async Task<IActionResult> RegistrarPagamento(int clubeId, Guid id, PagamentoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var item = await db.Mensalidades.SingleOrDefaultAsync(row => row.Id == id && row.ClubeId == clubeId, ct);
        if (item is null) return NotFound();
        if (item.Situacao == SituacaoMensalidade.Cancelada) return Conflict(new { message = "Uma mensalidade cancelada não pode receber baixa." });
        if (request.Paga && item.Situacao != SituacaoMensalidade.Paga)
        {
            item.Situacao = SituacaoMensalidade.Paga; item.PagaEmUtc = DateTime.UtcNow;
            db.Pagamentos.Add(new PagamentoMensalidade { MensalidadeId = item.Id, Valor = item.Valor, RegistradoPorUsuarioId = UserId, Forma = request.Forma?.Trim() ?? "" });
        }
        else if (!request.Paga) { item.Situacao = SituacaoMensalidade.Pendente; item.PagaEmUtc = null; }
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("dependentes")]
    public async Task<IActionResult> Dependentes(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.Dependentes.Where(row => row.TitularPessoaId == PersonId && row.Ativo).OrderBy(row => row.NomeCompleto).Select(row => new { row.Id, row.NomeCompleto, row.DataNascimento, row.Parentesco }).ToListAsync(ct));
    }

    [HttpPost("dependentes")]
    public async Task<IActionResult> CriarDependente(int clubeId, DependenteRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        if (!await db.Associacoes.AnyAsync(row => row.PessoaId == PersonId && row.ClubeId == clubeId && row.Situacao == SituacaoAssociacao.Ativa, ct)) return Forbid();
        var item = new Dependente { TitularPessoaId = PersonId, NomeCompleto = request.Nome.Trim(), DataNascimento = request.DataNascimento, Parentesco = request.Parentesco.Trim() };
        db.Dependentes.Add(item); await db.SaveChangesAsync(ct); return Created($"/api/clubes/{clubeId}/dependentes/{item.Id}", new { item.Id });
    }

    [HttpGet("pedidos-ingresso")]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public async Task<IActionResult> PedidosIngresso(int clubeId, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        return Ok(await db.SolicitacoesIngresso.Include(row => row.Pessoa).Where(row => row.ClubeId == clubeId && row.Situacao == SituacaoPedidoIngresso.Pendente).OrderBy(row => row.CriadoEmUtc).Select(row => new { row.Id, row.Pessoa.NomeCompleto, row.Pessoa.Cpf, categoria = row.Categoria.ToString(), row.Justificativa, row.CriadoEmUtc }).ToListAsync(ct));
    }

    [HttpPatch("pedidos-ingresso/{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Presidente")]
    public async Task<IActionResult> AvaliarIngresso(int clubeId, Guid id, AvaliacaoIngressoRequest request, CancellationToken ct)
    {
        if (!CanAccessClub(clubeId)) return Forbid();
        var item = await db.SolicitacoesIngresso.SingleOrDefaultAsync(row => row.Id == id && row.ClubeId == clubeId, ct);
        if (item is null) return NotFound();
        if (item.Situacao != SituacaoPedidoIngresso.Pendente) return Conflict(new { message = "Esse pedido já foi avaliado." });
        item.Situacao = request.Aprovar ? SituacaoPedidoIngresso.Aprovado : SituacaoPedidoIngresso.Recusado; item.AvaliadoPorUsuarioId = UserId; item.AvaliadoEmUtc = DateTime.UtcNow;
        if (request.Aprovar)
        {
            if (!Enum.IsDefined(item.Categoria)) return BadRequest(new { message = "Categoria de ingresso inválida." });
            try { await associations.AssociateToClubAsync(item.PessoaId, clubeId, (CategoriaAssociado)item.Categoria, 10, ct); }
            catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        }
        await db.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<bool> HasScheduleConflict(int clubId, int spaceId, DateTime start, DateTime end, CancellationToken ct, Guid? exceptReservation = null)
    {
        var eventConflict = await db.Eventos.AnyAsync(row => row.ClubeId == clubId && row.EspacoId == spaceId && row.Ativo && row.InicioUtc < end && row.FimUtc > start, ct);
        if (eventConflict) return true;
        return await db.Reservas.AnyAsync(row => row.ClubeId == clubId && row.EspacoId == spaceId && row.Situacao != SituacaoReserva.Recusada && row.Situacao != SituacaoReserva.Cancelada && row.Id != exceptReservation && row.InicioUtc < end && row.FimUtc > start, ct);
    }

    private Task<bool> IsActiveMemberAsync(int clubId, CancellationToken ct) => db.Associacoes.AnyAsync(row => row.ClubeId == clubId && row.PessoaId == PersonId && row.Situacao == SituacaoAssociacao.Ativa, ct);

    private static DateTime ToBrazilianLocal(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")); }
    }

    private static async Task<T> WithAgendaLocksAsync<T>(IEnumerable<string> keys, Func<Task<T>> action, CancellationToken ct)
    {
        var locks = keys.Select(key => (StringComparer.Ordinal.GetHashCode(key) & int.MaxValue) % AgendaLocks.Length)
            .Distinct().Order().Select(index => AgendaLocks[index]).ToArray();
        var acquired = 0;
        try
        {
            foreach (var gate in locks) { await gate.WaitAsync(ct); acquired++; }
            return await action();
        }
        finally
        {
            for (var index = acquired - 1; index >= 0; index--) locks[index].Release();
        }
    }

    public sealed record EventoRequest([param: Required, StringLength(160, MinimumLength = 2)] string Nome, [param: StringLength(2000)] string? Descricao, DateTime InicioUtc, DateTime FimUtc, [param: StringLength(160)] string? Local, bool PublicoGeral, bool Beneficente, int? EspacoId = null);
    public sealed record EspacoRequest([param: Required, StringLength(100, MinimumLength = 2)] string Nome, [param: StringLength(1000)] string? Descricao, [param: Required, StringLength(60)] string Tipo = "Espaço", int? Capacidade = null, TimeOnly? AbreAs = null, TimeOnly? FechaAs = null, bool PermiteDependentes = true);
    public sealed record ReservaRequest(Guid IdempotencyKey, int EspacoId, [param: Required, StringLength(300, MinimumLength = 3)] string Finalidade, DateTime InicioUtc, DateTime FimUtc, int? DependenteId = null, List<int>? Participantes = null, List<string>? Convidados = null);
    public sealed record AvaliacaoReservaRequest(bool Aprovar, [StringLength(500)] string? Observacao);
    public sealed record ManifestacaoRequest(TipoManifestacao Tipo, [param: Required, StringLength(160, MinimumLength = 3)] string Assunto, [param: Required, StringLength(2000, MinimumLength = 5)] string Descricao);
    public sealed record AtualizarManifestacaoRequest(SituacaoManifestacao Situacao);
    public sealed record ValorMensalidadeRequest(CategoriaAssociado Categoria, decimal Valor, DateOnly VigenteDesde);
    public sealed record GerarMensalidadesRequest(int Ano, int Mes, DateOnly? Vencimento = null);
    public sealed record InteressesRequest(List<string>? Nomes);
    public sealed record DiasVencimentoRequest(List<int>? Dias);
    public sealed record EscolherDiaVencimentoRequest(int Dia);
    public sealed record PagamentoRequest(bool Paga, [param: StringLength(40)] string? Forma);
    public sealed record DependenteRequest([param: Required, StringLength(160, MinimumLength = 2)] string Nome, DateOnly? DataNascimento, [param: Required, StringLength(40)] string Parentesco);
    public sealed record AvaliacaoIngressoRequest(bool Aprovar);
}
