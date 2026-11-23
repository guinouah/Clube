using System.Collections.ObjectModel;
using Clubes.Mobile.Core.Infrastructure;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile.Core.ViewModels;

public sealed class DuesViewModel(IClubesApi api) : ObservableObject
{
    private string _status = "Entre online para carregar suas mensalidades.";
    private bool _isBusy;

    public ObservableCollection<DuesDto> Dues { get; } = [];
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public async Task LoadAsync(int? clubId, int? personId, CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        if (clubId is null || personId is null) { Dues.Clear(); Status = "Entre na sua conta para consultar suas mensalidades."; return; }

        IsBusy = true;
        try
        {
            var items = await api.GetDuesAsync(clubId.Value, personId.Value, cancellationToken);
            Dues.Clear();
            foreach (var item in items) Dues.Add(item);

            var syncedAt = await api.GetLastSyncAsync($"dues:{personId.Value}:{clubId.Value}");
            Status = syncedAt is null
                ? items.Count == 0 ? "Sem mensalidades em cache. Conecte-se para carregar a situação oficial." : "Exibindo dados disponíveis no aparelho."
                : $"Última sincronização: {syncedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}. Consulta somente leitura; os dados podem estar desatualizados.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Status = "Não foi possível carregar as mensalidades. Tente novamente quando houver conexão.";
        }
        finally { IsBusy = false; }
    }
}
