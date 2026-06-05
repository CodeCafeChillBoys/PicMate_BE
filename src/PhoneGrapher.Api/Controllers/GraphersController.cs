using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/graphers")]
public sealed class GraphersController(IGrapherService grapherService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GrapherSummaryResponse>>> Search(
        [FromQuery] string? location,
        [FromQuery] string? style,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minRating,
        [FromQuery] bool? verified,
        CancellationToken cancellationToken)
    {
        var request = new GrapherSearchRequest(location, style, minPrice, maxPrice, minRating, verified);
        return Ok(await grapherService.SearchAsync(request, cancellationToken));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<GrapherDetailResponse>> GetProfile(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await grapherService.GetProfileAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("me")]
    [Authorize(Roles = "Grapher")]
    public async Task<IActionResult> UpsertMyProfile(
        [FromBody] UpsertGrapherProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await grapherService.UpsertProfileAsync(User.GetUserId(), request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "UpsertException: " + ex.ToString() });
        }
    }

    [HttpPost("me/seed-packages")]
    [Authorize(Roles = "Grapher")]
    public async Task<ActionResult<IReadOnlyList<ServicePackageResponse>>> SeedDefaultPackages(
        CancellationToken cancellationToken)
    {
        return Ok(await grapherService.SeedDefaultPackagesAsync(User.GetUserId(), cancellationToken));
    }
}
