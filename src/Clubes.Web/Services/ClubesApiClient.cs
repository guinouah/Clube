using System.Net.Http.Json;
using Clubes.Web.Models;

namespace Clubes.Web.Services;

public sealed class ClubesApiClient(HttpClient httpClient)
{
    public async Task<ApiStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<HealthResponse>("api/health", cancellationToken);
            return response?.Status == "online"
                ? new ApiStatusViewModel(true, response.DatabaseConfigured, response.DatabaseConfigured ? "API conectada e banco local pronto" : "API conectada, mas o banco não está configurado")
                : new ApiStatusViewModel(false, false, "A API respondeu com um estado inesperado.");
        }
        catch (HttpRequestException)
        {
            return new ApiStatusViewModel(false, false, "API indisponível. Inicie a API e tente novamente.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiStatusViewModel(false, false, "A API demorou para responder.");
        }
    }

    private sealed record HealthResponse(string Status, string Service, bool DatabaseConfigured);
}
