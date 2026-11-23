using System.ComponentModel.DataAnnotations;

namespace Clubes.Web.Models;

public sealed class LoginViewModel
{
    [Required, StringLength(14, MinimumLength = 11)] public string Cpf { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Senha { get; set; } = "";
    public int? ClubeId { get; set; }
    public List<ClubeOption> Clubes { get; set; } = [];
    public string? Erro { get; set; }
}

public sealed record ClubeOption(int Id, string Nome, string Sigla);
