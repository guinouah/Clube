namespace Clubes.Api.Models;

public sealed class Manifestacao
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int ClubeId { get; set; }

    public int AutorPessoaId { get; set; }

    public TipoManifestacao Tipo { get; set; }

    public string Assunto { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public SituacaoManifestacao Situacao { get; set; } = SituacaoManifestacao.Aberta;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public int? ResponsavelUsuarioId { get; set; }
}

public enum TipoManifestacao
{
    Solicitacao = 1,
    Reclamacao = 2
}

public enum SituacaoManifestacao
{
    Aberta = 1,
    EmAtendimento = 2,
    Concluida = 3
}

public sealed class RespostaManifestacao
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ManifestacaoId { get; set; }

    public int AutorUsuarioId { get; set; }

    public string Mensagem { get; set; } = string.Empty;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;
}
