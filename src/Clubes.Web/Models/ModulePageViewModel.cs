namespace Clubes.Web.Models;

public sealed record ModulePageViewModel(
    string Slug,
    string Title,
    string Description,
    string Icon,
    string Role,
    string NextStep,
    List<IReadOnlyDictionary<string, string>> Rows,
    string? Error = null,
    List<SpaceOption>? Spaces = null);

public sealed record SpaceOption(int Id, string Nome);
