using IplStore.Domain.Catalogue;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[Route("api/v1/franchises")]
public sealed class FranchisesController : ApiControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<Franchise>> List()
    {
        return Ok(Franchises.All);
    }
}
