namespace Clubes.Api.Models;

public sealed class AssociacaoClube
{
    public int PessoaId { get; set; }

    public Pessoa Pessoa { get; set; } = null!;

    public int ClubeId { get; set; }

    public Clube Clube { get; set; } = null!;

    public CategoriaAssociado Categoria { get; set; }

    public SituacaoAssociacao Situacao { get; set; } = SituacaoAssociacao.Ativa;

    public DateTime IngressouEmUtc { get; set; } = DateTime.UtcNow;

    public int DiaVencimento { get; set; } = 10;
}

public enum CategoriaAssociado
{
    FundadorAssociado = 1,
    Convidado = 2,
    Simpatizante = 3
}

public enum SituacaoAssociacao
{
    Ativa = 1,
    Inativa = 2
}
