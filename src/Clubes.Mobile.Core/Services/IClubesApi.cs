namespace Clubes.Mobile.Core.Services;

public interface IClubesApi
{
    Task<ApiConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string cpf, string password, int? clubId, CancellationToken cancellationToken = default);
    void SetAccessToken(string? token);
    Task<IReadOnlyList<ClubEventDto>> GetEventsAsync(int clubId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DuesDto>> GetDuesAsync(int clubId, int personId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpaceDto>> GetSpacesAsync(int clubId, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLastSyncAsync(string dataKey);
    Task<IReadOnlyList<StateDto>> GetStatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CityDto>> GetCitiesAsync(string uf, CancellationToken cancellationToken = default);
    Task<QueueResult> QueueActionAsync(int clubId, int personId, string endpoint, string jsonPayload, CancellationToken cancellationToken = default);
    Task<SyncResult> SynchronizeAsync(int clubId, int personId, CancellationToken cancellationToken = default);
}

public sealed record ApiConnectionResult(bool IsOnline, string Message);
public sealed record LoginResult(bool Success, string Message, string? Token, int? ClubId, int PersonId, IReadOnlyList<int> ClubIds);
public sealed record StateDto(string Sigla, string Nome);
public sealed record CityDto(long CodigoIbge, string Nome);
public sealed record ClubEventDto(int Id, string Nome, string Descricao, DateTime InicioUtc, DateTime FimUtc, string Local, bool PublicoGeral, bool Beneficente)
{
    public DateTime InicioLocal => InicioUtc.ToLocalTime();
    public string PublicoDescricao => Beneficente ? "Evento beneficente" : PublicoGeral ? "Aberto ao público" : "Exclusivo para associados";
}
public sealed record DuesDto(Guid Id, int Ano, int Mes, DateOnly Vencimento, decimal Valor, string Situacao, DateTime? PagaEmUtc)
{
    public string Competencia => $"{Mes:00}/{Ano}";
    public string CompetenciaTexto => $"Competência {Competencia}";
    public string ValorFormatado => Valor.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}
public sealed record SpaceDto(int Id, string Nome, string Descricao);
public sealed record QueueResult(Guid Id, string Estado, string Mensagem);
public sealed record SyncResult(int Enviados, int Falhas, string Mensagem);
