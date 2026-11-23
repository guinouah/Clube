using Clubes.Api.Services;
using Xunit;

namespace Clubes.UnitTests;

public sealed class ValidationTests
{
    [Fact]
    public void ValidCpf_IsNormalized()
    {
        Assert.Equal("52998224725", CpfValidator.NormalizeAndValidate("529.982.247-25"));
    }

    [Theory]
    [InlineData("111.111.111-11")]
    [InlineData("529.982.247-24")]
    [InlineData("123")]
    public void InvalidCpf_IsRejected(string cpf)
    {
        Assert.Throws<ArgumentException>(() => CpfValidator.NormalizeAndValidate(cpf));
    }

    [Fact]
    public void StateCatalog_ContainsAll27BrazilianUnitsAndUppercaseCodes()
    {
        var states = LocalidadesService.Estados;
        Assert.Equal(27, states.Count);
        Assert.Contains(states, state => state.Sigla == "PR" && state.Nome == "Paraná");
        Assert.Contains(states, state => state.Sigla == "DF" && state.Nome == "Distrito Federal");
        Assert.All(states, state => Assert.Equal(state.Sigla, state.Sigla.ToUpperInvariant()));
    }
}
