using Clubes.Mobile.Core.Infrastructure;
using Clubes.Mobile.Core.Services;

namespace Clubes.Mobile.Core.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IClubesApi _api;
    private string _connectionMessage = "Aguardando verificação da API.";
    private bool _isApiOnline;

    public DashboardViewModel(IClubesApi api)
    {
        _api = api;
        RefreshCommand = new AsyncCommand(() => RefreshAsync());
    }

    public string ConnectionMessage
    {
        get => _connectionMessage;
        private set => SetProperty(ref _connectionMessage, value);
    }

    public bool IsApiOnline
    {
        get => _isApiOnline;
        private set => SetProperty(ref _isApiOnline, value);
    }

    public AsyncCommand RefreshCommand { get; }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ConnectionMessage = "Verificando conexão com o clube…";
        var result = await _api.CheckConnectionAsync(cancellationToken);
        IsApiOnline = result.IsOnline;
        ConnectionMessage = result.Message;
    }
}
