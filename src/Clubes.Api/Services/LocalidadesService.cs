using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Clubes.Api.Services;

public sealed class LocalidadesService(IHttpClientFactory clients, IMemoryCache cache)
{
    private static readonly JsonSerializerOptions IbgeJsonOptions = new(JsonSerializerDefaults.Web);
    public static readonly IReadOnlyList<Estado> Estados =
    [
        new("AC", "Acre"), new("AL", "Alagoas"), new("AP", "Amapá"), new("AM", "Amazonas"), new("BA", "Bahia"),
        new("CE", "Ceará"), new("DF", "Distrito Federal"), new("ES", "Espírito Santo"), new("GO", "Goiás"),
        new("MA", "Maranhão"), new("MT", "Mato Grosso"), new("MS", "Mato Grosso do Sul"), new("MG", "Minas Gerais"),
        new("PA", "Pará"), new("PB", "Paraíba"), new("PR", "Paraná"), new("PE", "Pernambuco"), new("PI", "Piauí"),
        new("RJ", "Rio de Janeiro"), new("RN", "Rio Grande do Norte"), new("RS", "Rio Grande do Sul"), new("RO", "Rondônia"),
        new("RR", "Roraima"), new("SC", "Santa Catarina"), new("SP", "São Paulo"), new("SE", "Sergipe"), new("TO", "Tocantins")
    ];

    public bool IsValidState(string? uf) => uf is not null && Estados.Any(state => state.Sigla.Equals(uf.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<Municipio>> GetMunicipiosAsync(string uf, CancellationToken ct)
    {
        var state = Estados.SingleOrDefault(item => item.Sigla.Equals(uf, StringComparison.OrdinalIgnoreCase));
        if (state is null) throw new ArgumentException("Informe uma sigla de estado brasileiro válida.", nameof(uf));
        return await cache.GetOrCreateAsync($"municipios:{state.Sigla}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            using var response = await clients.CreateClient("ibge").GetAsync($"estados/{state.Sigla}/municipios?orderBy=nome", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<List<Municipio>>(stream, IbgeJsonOptions, cancellationToken: ct) ?? [];
        }) ?? [];
    }

    public async Task<bool> IsValidMunicipioAsync(string? uf, string? city, CancellationToken ct)
    {
        if (!IsValidState(uf) || string.IsNullOrWhiteSpace(city)) return false;
        try { return (await GetMunicipiosAsync(uf!, ct)).Any(item => item.Nome.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase)); }
        catch (HttpRequestException) { return false; }
    }

    public sealed record Estado(string Sigla, string Nome);
    public sealed record Municipio(long Id, string Nome);
}
