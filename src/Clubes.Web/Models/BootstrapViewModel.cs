using System.ComponentModel.DataAnnotations;

namespace Clubes.Web.Models;

public sealed class BootstrapViewModel
{
    [Required, StringLength(14, MinimumLength = 11, ErrorMessage = "Informe o CPF completo.")]
    public string Cpf { get; set; } = "";

    [Required, StringLength(160, MinimumLength = 2)]
    public string Nome { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Required, MinLength(12, ErrorMessage = "Use uma senha com pelo menos 12 caracteres."), DataType(DataType.Password)]
    public string Senha { get; set; } = "";

    [Required, Compare(nameof(Senha), ErrorMessage = "As senhas não coincidem."), DataType(DataType.Password)]
    public string ConfirmarSenha { get; set; } = "";

    public string? Erro { get; set; }
    public bool Criado { get; set; }
}
