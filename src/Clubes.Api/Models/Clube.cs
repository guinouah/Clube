namespace Clubes.Api.Models;

public sealed class Clube
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Sigla { get; set; } = string.Empty;

    public string Endereco { get; set; } = string.Empty;

    public string Bairro { get; set; } = string.Empty;

    public string Cidade { get; set; } = string.Empty;

    public string Uf { get; set; } = string.Empty;

    public string Telefone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AssociacaoClube> Associacoes { get; set; } = [];
}
