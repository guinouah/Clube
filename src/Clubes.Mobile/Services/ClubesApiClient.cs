using System.Net.Http.Json;
using System.Text.Json;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile.Services;

public sealed class ClubesApiClient(HttpClient httpClient, OfflineStore offlineStore) : IClubesApi
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public async Task<ApiConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<HealthResponse>("api/health", cancellationToken);
            return response?.Status == "online"
                ? new ApiConnectionResult(true, "API conectada. Os dados oficiais serão carregados após o login.")
                : new ApiConnectionResult(false, "A API respondeu com um estado inesperado.");
        }
        catch (HttpRequestException)
        {
            return new ApiConnectionResult(false, "API desconectada. Verifique se o serviço está aberto.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiConnectionResult(false, "A API demorou para responder. Tente novamente.");
        }
    }

    public void SetAccessToken(string? token)
    {
        httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token) ? null : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<LoginResult> LoginAsync(string cpf, string password, int? clubId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/auth/login", new { cpf, senha = password, clubeId = clubId }, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!response.IsSuccessStatusCode)
                return new(false, document.RootElement.TryGetProperty("message", out var error) ? error.GetString() ?? "Não foi possível entrar." : "CPF ou senha inválidos.", null, null, 0, []);
            var root = document.RootElement;
            return new(true, "Acesso autorizado.", root.GetProperty("accessToken").GetString(), root.GetProperty("activeClubId").ValueKind == JsonValueKind.Number ? root.GetProperty("activeClubId").GetInt32() : null, root.GetProperty("personId").GetInt32(), root.GetProperty("availableClubIds").EnumerateArray().Select(item => item.GetInt32()).ToArray());
        }
        catch (HttpRequestException) { return new(false, "Sem conexão com a API. Entre online ao menos uma vez para habilitar o uso offline.", null, null, 0, []); }
    }

    public async Task<IReadOnlyList<ClubEventDto>> GetEventsAsync(int clubId, CancellationToken cancellationToken = default)
    {
        var key = $"events:{clubId}";
        try
        {
            var items = await httpClient.GetFromJsonAsync<List<ClubEventDto>>($"api/clubes/{clubId}/eventos", cancellationToken) ?? [];
            await offlineStore.SaveCacheAsync(key, JsonSerializer.Serialize(items, _json)); return items;
        }
        catch (HttpRequestException) { return await ReadCacheAsync<ClubEventDto>(key); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return await ReadCacheAsync<ClubEventDto>(key); }
    }

    public async Task<IReadOnlyList<DuesDto>> GetDuesAsync(int clubId, int personId, CancellationToken cancellationToken = default)
    {
        var key = $"dues:{personId}:{clubId}";
        try
        {
            var items = await httpClient.GetFromJsonAsync<List<DuesDto>>($"api/clubes/{clubId}/mensalidades/minhas", cancellationToken) ?? [];
            await offlineStore.SaveCacheAsync(key, JsonSerializer.Serialize(items, _json)); return items;
        }
        catch (HttpRequestException) { return await ReadCacheAsync<DuesDto>(key); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return await ReadCacheAsync<DuesDto>(key); }
    }

    public async Task<IReadOnlyList<SpaceDto>> GetSpacesAsync(int clubId, CancellationToken cancellationToken = default)
    {
        var key = $"spaces:{clubId}";
        try
        {
            var items = await httpClient.GetFromJsonAsync<List<SpaceDto>>($"api/clubes/{clubId}/espacos", cancellationToken) ?? [];
            await offlineStore.SaveCacheAsync(key, JsonSerializer.Serialize(items, _json)); return items;
        }
        catch (HttpRequestException) { return await ReadCacheAsync<SpaceDto>(key); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return await ReadCacheAsync<SpaceDto>(key); }
    }

    public Task<DateTime?> GetLastSyncAsync(string dataKey) => offlineStore.GetCacheTimeAsync(dataKey);

    public async Task<IReadOnlyList<StateDto>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        var key = "states:br";
        try
        {
            var rows = await httpClient.GetFromJsonAsync<List<StateDto>>("api/localidades/estados", cancellationToken) ?? [];
            await offlineStore.SaveCacheAsync(key, JsonSerializer.Serialize(rows, _json)); return rows;
        }
        catch (HttpRequestException) { return await ReadCacheAsync<StateDto>(key); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return await ReadCacheAsync<StateDto>(key); }
    }

    public async Task<IReadOnlyList<CityDto>> GetCitiesAsync(string uf, CancellationToken cancellationToken = default)
    {
        var key = $"cities:{uf.ToUpperInvariant()}";
        try
        {
            var rows = await httpClient.GetFromJsonAsync<List<CityDto>>($"api/localidades/estados/{Uri.EscapeDataString(uf)}/municipios", cancellationToken) ?? [];
            await offlineStore.SaveCacheAsync(key, JsonSerializer.Serialize(rows, _json)); return rows;
        }
        catch (HttpRequestException) { return await ReadCacheAsync<CityDto>(key); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return await ReadCacheAsync<CityDto>(key); }
    }

    public async Task<QueueResult> QueueActionAsync(int clubId, int personId, string endpoint, string jsonPayload, CancellationToken cancellationToken = default)
    {
        var reservationsPath = $"api/clubes/{clubId}/reservas";
        var requestsPath = $"api/clubes/{clubId}/solicitacoes";
        if (endpoint != reservationsPath && endpoint != requestsPath)
            throw new ArgumentException("A fila offline aceita apenas reservas e solicitações do clube ativo.", nameof(endpoint));
        var id = await offlineStore.EnqueueAsync(clubId, personId, endpoint, jsonPayload);
        return new(id, "Aguardando envio", "A ação foi guardada neste aparelho. A reserva ainda não está confirmada.");
    }

    public async Task<SyncResult> SynchronizeAsync(int clubId, int personId, CancellationToken cancellationToken = default)
    {
        var pending = await offlineStore.GetPendingAsync(personId); var sent = 0; var failures = 0;
        foreach (var item in pending.Where(item => item.ClubId == clubId))
        {
            using var content = new StringContent(item.Json, System.Text.Encoding.UTF8, "application/json");
            try
            {
                using var response = await httpClient.PostAsync(item.Endpoint, content, cancellationToken);
                if (response.IsSuccessStatusCode) { item.Status = "Recebido pela API"; item.Error = null; sent++; }
                else if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    item.Error = "Entre novamente com a conta que criou este pedido para retomar a sincronização.";
                    failures++;
                }
                else if ((int)response.StatusCode is >= 400 and < 500) { item.Status = "Recusado pela API"; item.Error = await response.Content.ReadAsStringAsync(cancellationToken); failures++; }
                else { item.Error = $"API respondeu {(int)response.StatusCode}; nova tentativa será feita depois."; failures++; }
                await offlineStore.UpdateActionAsync(item);
            }
            catch (HttpRequestException) { item.Error = "Sem conexão; item continua aguardando envio."; await offlineStore.UpdateActionAsync(item); failures++; }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { item.Error = "A API demorou para responder; o item continua aguardando nova tentativa."; await offlineStore.UpdateActionAsync(item); failures++; }
        }
        return new(sent, failures, failures == 0 ? "Fila sincronizada." : "Algumas ações continuam na fila ou precisam ser revisadas.");
    }

    private async Task<IReadOnlyList<T>> ReadCacheAsync<T>(string key)
    {
        var entry = await offlineStore.GetCacheAsync(key);
        return entry.Json is null ? [] : JsonSerializer.Deserialize<List<T>>(entry.Json, _json) ?? [];
    }

    private sealed record HealthResponse(string Status, string Service);
}
