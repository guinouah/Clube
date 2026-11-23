namespace Clubes.Api.Models;

public sealed class EventoClube
{
    public int Id { get; set; }

    public int ClubeId { get; set; }

    public Clube Clube { get; set; } = null!;

    public int? EspacoId { get; set; }

    public EspacoClube? Espaco { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    public string Local { get; set; } = string.Empty;

    public bool PublicoGeral { get; set; }

    public bool Beneficente { get; set; }

    public bool Ativo { get; set; } = true;
}
