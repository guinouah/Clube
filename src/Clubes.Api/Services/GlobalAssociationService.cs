using Clubes.Api.Data;
using Clubes.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Clubes.Api.Services;

public sealed class ClubMembershipService(ClubesDbContext db)
{
    public async Task AssociateToClubAsync(int pessoaId, int clubeId, CategoriaAssociado categoria, int diaVencimento, CancellationToken cancellationToken)
    {
        var association = await db.Associacoes.SingleOrDefaultAsync(row => row.PessoaId == pessoaId && row.ClubeId == clubeId, cancellationToken);
        if (association is null)
        {
            if (await db.Associacoes.AnyAsync(row => row.PessoaId == pessoaId, cancellationToken))
                throw new InvalidOperationException("Este CPF já possui vínculo com outro clube. Cada pessoa pode participar de um clube por vez.");
            db.Associacoes.Add(new AssociacaoClube { PessoaId = pessoaId, ClubeId = clubeId, Categoria = categoria, Situacao = SituacaoAssociacao.Ativa, DiaVencimento = diaVencimento });
        }
        else
        {
            association.Situacao = SituacaoAssociacao.Ativa;
            association.Categoria = categoria;
            association.DiaVencimento = diaVencimento;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
