using Clubes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clubes.Api.Controllers;

[ApiController]
[Route("api/localidades")]
[AllowAnonymous]
public sealed class LocalidadesController(LocalidadesService localidades) : ControllerBase
{
    [HttpGet("estados")]
    public IActionResult ListStates() => Ok(LocalidadesService.Estados);

    [HttpGet("estados/{uf}/municipios")]
    public async Task<IActionResult> ListCities(string uf, CancellationToken cancellationToken)
    {
        if (!localidades.IsValidState(uf)) return BadRequest(new { message = "Informe uma sigla de estado brasileiro válida." });
        try { return Ok(await localidades.GetMunicipiosAsync(uf, cancellationToken)); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "A lista oficial de municípios está temporariamente indisponível." }); }
    }
}
