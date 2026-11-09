using Microsoft.AspNetCore.Mvc;
using UserApi.Models;
using UserApi.Services;

namespace UserApi.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly AdmissionService _service;

    public MeController(AdmissionService service)
    {
        _service = service;
    }

    [HttpGet("holds")]
    [ProducesResponseType(typeof(IEnumerable<MyHoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MyHolds([FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(new ErrorResponse("BAD_REQUEST", "X-Student-Id header is required."));

        var holds = await _service.GetMyHoldsAsync(studentId);
        return Ok(holds);
    }
}
