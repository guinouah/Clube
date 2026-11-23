namespace Clubes.Web.Models;

public sealed record ApiStatusViewModel(bool Online, bool DatabaseConfigured, string Message);
