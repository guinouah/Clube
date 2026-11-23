using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile.Services;

/// <summary>Retries queued member actions when the app starts online or connectivity returns.</summary>
public sealed class OfflineSyncCoordinator(IClubesApi api, OfflineStore store) : IDisposable
{
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private bool _started;

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        try
        {
            await store.InitializeAsync();
            if (HasInternet()) await SynchronizeActiveClubAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Offline queue startup failed: {ex.Message}");
        }
    }

    public Task SynchronizeNowAsync() => SynchronizeActiveClubAsync();

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        if (args.NetworkAccess is not (NetworkAccess.Internet or NetworkAccess.ConstrainedInternet)) return;
        await SynchronizeActiveClubAsync();
    }

    private async Task SynchronizeActiveClubAsync()
    {
        if (!HasInternet() || !await _syncGate.WaitAsync(0)) return;
        try
        {
            var token = await SecureStorage.Default.GetAsync("access_token");
            if (string.IsNullOrWhiteSpace(token)) return;
            if (!int.TryParse(await SecureStorage.Default.GetAsync("active_club_id"), out var clubId)) return;
            if (!int.TryParse(await SecureStorage.Default.GetAsync("person_id"), out var personId)) return;

            api.SetAccessToken(token);
            var result = await api.SynchronizeAsync(clubId, personId);
            if (result.Enviados > 0 || result.Falhas > 0)
                System.Diagnostics.Debug.WriteLine($"Offline queue: sent {result.Enviados}; pending or rejected {result.Falhas}.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Offline queue sync failed: {ex.Message}");
        }
        finally { _syncGate.Release(); }
    }

    private static bool HasInternet() => Connectivity.Current.NetworkAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;

    public void Dispose()
    {
        if (_started) Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _syncGate.Dispose();
    }
}
