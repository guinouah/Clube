using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Clubes.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected int PersonId => int.Parse(User.FindFirstValue("person_id")!);

    protected int? SelectedClubId => int.TryParse(User.FindFirstValue("club_id"), out var value) ? value : null;

    protected bool IsSuperAdmin => User.IsInRole("SuperAdmin");

    protected bool CanAccessClub(int clubId) => IsSuperAdmin || SelectedClubId == clubId;

    protected bool IsPresident => User.IsInRole("Presidente") || IsSuperAdmin;

    protected bool IsTreasurer => User.IsInRole("Tesoureiro") || IsSuperAdmin;
}
