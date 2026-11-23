namespace Clubes.Api.Models;

public sealed class EspacoClube
{
    public int Id { get; set; }

    public int ClubeId { get; set; }

    public Clube Clube { get; set; } = null!;

    public string Nome { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Tipo { get; set; } = "Espaço";

    public int? Capacidade { get; set; }

    public TimeOnly AbreAs { get; set; } = new(8, 0);

    public TimeOnly FechaAs { get; set; } = new(22, 0);

    public bool PermiteDependentes { get; set; } = true;

    public bool Ativo { get; set; } = true;
}

public sealed class Reserva
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IdempotencyKey { get; set; }

    public int ClubeId { get; set; }

    public int EspacoId { get; set; }

    public EspacoClube Espaco { get; set; } = null!;

    public int PessoaId { get; set; }

    public int? DependenteId { get; set; }

    public string Finalidade { get; set; } = string.Empty;

    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    public SituacaoReserva Situacao { get; set; } = SituacaoReserva.Pendente;

    public int? AvaliadoPorUsuarioId { get; set; }

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public string? ObservacaoAvaliacao { get; set; }

    public ICollection<ReservaParticipante> Participantes { get; set; } = [];
}

public sealed class ReservaParticipante
{
    public int Id { get; set; }
    public Guid ReservaId { get; set; }
    public Reserva Reserva { get; set; } = null!;
    public int? PessoaId { get; set; }
    public int? DependenteId { get; set; }
    public string NomeConvidado { get; set; } = string.Empty;
}

public enum SituacaoReserva
{
    Pendente = 1,
    Aprovada = 2,
    Recusada = 3,
    Cancelada = 4
}
