using System.ComponentModel.DataAnnotations;

namespace Clubes.Web.Models;

public sealed class ClubeFormViewModel
{
    [Required, StringLength(160, MinimumLength = 2)] public string Nome { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 2)] public string Sigla { get; set; } = "";
    [StringLength(200)] public string Endereco { get; set; } = "";
    [StringLength(100)] public string Bairro { get; set; } = "";
    [Required] public string Uf { get; set; } = "";
    [Required] public string Cidade { get; set; } = "";
    [Phone, StringLength(30)] public string Telefone { get; set; } = "";
    [EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    public List<EstadoOption> Estados { get; set; } = [];
    public List<string> Municipios { get; set; } = [];
    public string? Erro { get; set; }
}
public sealed record EstadoOption(string Sigla, string Nome);
