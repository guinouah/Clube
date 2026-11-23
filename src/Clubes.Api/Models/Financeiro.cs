namespace Clubes.Api.Models;

public sealed class ValorMensalidade
{
    public int Id { get; set; }

    public int ClubeId { get; set; }

    public CategoriaAssociado Categoria { get; set; }

    public decimal Valor { get; set; }

    public DateOnly VigenteDesde { get; set; }

    public bool Ativo { get; set; } = true;
}

public sealed class DiaVencimentoClube
{
    public int ClubeId { get; set; }
    public int Dia { get; set; }
}

public sealed class Mensalidade
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int ClubeId { get; set; }

    public int PessoaId { get; set; }

    public Pessoa Pessoa { get; set; } = null!;

    public int Ano { get; set; }

    public int Mes { get; set; }

    public DateOnly Vencimento { get; set; }

    public decimal Valor { get; set; }

    public SituacaoMensalidade Situacao { get; set; } = SituacaoMensalidade.Pendente;

    public DateTime? PagaEmUtc { get; set; }

    public int? RegistradaPorUsuarioId { get; set; }
}

public enum SituacaoMensalidade
{
    Pendente = 1,
    Paga = 2,
    Atrasada = 3,
    Cancelada = 4
}

public sealed class PagamentoMensalidade
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MensalidadeId { get; set; }

    public decimal Valor { get; set; }

    public DateTime RegistradoEmUtc { get; set; } = DateTime.UtcNow;

    public int RegistradoPorUsuarioId { get; set; }

    public string Forma { get; set; } = string.Empty;
}
