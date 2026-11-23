namespace Clubes.Api.Models;

public sealed class Pessoa
{
    public int Id { get; set; }

    public string Cpf { get; set; } = string.Empty;

    public string NomeCompleto { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Telefone { get; set; } = string.Empty;

    public string Endereco { get; set; } = string.Empty;

    public string Bairro { get; set; } = string.Empty;

    public string Cidade { get; set; } = string.Empty;

    public string Uf { get; set; } = string.Empty;

    public DateOnly? DataNascimento { get; set; }

    public string Sexo { get; set; } = string.Empty;

    public string EstadoCivil { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public Usuario? Usuario { get; set; }

    public ICollection<AssociacaoClube> Associacoes { get; set; } = [];

    public ICollection<Dependente> Dependentes { get; set; } = [];

    public ICollection<PessoaInteresse> Interesses { get; set; } = [];
}

public sealed class Interesse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public ICollection<PessoaInteresse> Pessoas { get; set; } = [];
}

public sealed class PessoaInteresse
{
    public int PessoaId { get; set; }
    public Pessoa Pessoa { get; set; } = null!;
    public int InteresseId { get; set; }
    public Interesse Interesse { get; set; } = null!;
}
