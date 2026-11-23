using System.Collections.ObjectModel;
using Clubes.Mobile.Core.Infrastructure;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile.Core.ViewModels;

public sealed class EventsViewModel(IClubesApi api) : ObservableObject
{
    private string _status = "Entre na sua conta para consultar a agenda.";
    private bool _isBusy;

    public ObservableCollection<ClubEventDto> Events { get; } = [];
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public async Task LoadAsync(int? clubId, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        if (clubId is null) { Events.Clear(); Status = "Entre na sua conta para consultar a agenda."; return; }

        IsBusy = true;
        try
        {
            var items = await api.GetEventsAsync(clubId.Value, cancellationToken);
            Events.Clear();
            foreach (var item in items) Events.Add(item);

            var syncedAt = await api.GetLastSyncAsync($"events:{clubId.Value}");
            Status = syncedAt is null
                ? items.Count == 0 ? "Sem eventos em cache. Conecte-se para carregar a agenda." : "Exibindo dados disponíveis no aparelho."
                : $"Última sincronização: {syncedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}. Os dados podem estar desatualizados.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Status = "Não foi possível carregar os eventos. Tente novamente quando houver conexão.";
        }
        finally { IsBusy = false; }
    }
}
