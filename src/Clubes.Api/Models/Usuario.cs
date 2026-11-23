namespace Clubes.Api.Models;

public sealed class Usuario
{
    public int Id { get; set; }

    public int PessoaId { get; set; }

    public Pessoa Pessoa { get; set; } = null!;

    public string SenhaHash { get; set; } = string.Empty;

    public bool SuperAdmin { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<VinculoUsuarioClube> Vinculos { get; set; } = [];
}

public enum PerfilClube
{
    Presidente = 1,
    Tesoureiro = 2,
    Associado = 3
}

public enum CategoriaSolicitada
{
    FundadorAssociado = 1,
    Convidado = 2,
    Simpatizante = 3
}

public enum SituacaoPedidoIngresso
{
    Pendente = 1,
    Aprovado = 2,
    Recusado = 3
}

public sealed class VinculoUsuarioClube
{
    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public int ClubeId { get; set; }

    public Clube Clube { get; set; } = null!;

    public PerfilClube Perfil { get; set; }
}

public sealed class SessaoUsuario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public int? ClubeId { get; set; }

    public string Perfil { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiraEmUtc { get; set; }

    public DateTime? RevogadaEmUtc { get; set; }
}

public sealed class SolicitacaoIngresso
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int PessoaId { get; set; }

    public Pessoa Pessoa { get; set; } = null!;

    public int ClubeId { get; set; }

    public Clube Clube { get; set; } = null!;

    public CategoriaSolicitada Categoria { get; set; }

    public SituacaoPedidoIngresso Situacao { get; set; } = SituacaoPedidoIngresso.Pendente;

    public int? AvaliadoPorUsuarioId { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public DateTime? AvaliadoEmUtc { get; set; }
}

public sealed class Dependente
{
    public int Id { get; set; }

    public int TitularPessoaId { get; set; }

    public Pessoa Titular { get; set; } = null!;

    public string NomeCompleto { get; set; } = string.Empty;

    public DateOnly? DataNascimento { get; set; }

    public string Parentesco { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
}
