using Microsoft.AspNetCore.Mvc;
using UserApi.Models;
using UserApi.Services;

namespace UserApi.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly AdmissionService _service;

    public HoldsController(AdmissionService service)
    {
        _service = service;
    }

    [HttpGet("{holdId}")]
    [ProducesResponseType(typeof(HoldStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string holdId)
    {
        var result = await _service.GetHoldAsync(holdId);
        if (result is null)
            return NotFound(new ErrorResponse("NOT_FOUND", "Hold not found."));
        return Ok(result);
    }

    [HttpPost("{holdId}/confirm")]
    [ProducesResponseType(typeof(ConfirmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(string holdId, [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(new ErrorResponse("BAD_REQUEST", "X-Student-Id header is required."));

        var result = await _service.ConfirmHoldAsync(holdId, studentId);
        return result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.NotFound => NotFound(new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Conflict => Conflict(new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse(result.Error!, result.Message!)),
            _ => BadRequest(new ErrorResponse(result.Error ?? "BAD_REQUEST", result.Message ?? "Invalid request."))
        };
    }

    [HttpDelete("{holdId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string holdId, [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(new ErrorResponse("BAD_REQUEST", "X-Student-Id header is required."));

        var result = await _service.CancelHoldAsync(holdId, studentId);
        return result.Status switch
        {
            ResultStatus.Ok => NoContent(),
            ResultStatus.NotFound => NotFound(new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse(result.Error!, result.Message!)),
            _ => BadRequest(new ErrorResponse(result.Error ?? "BAD_REQUEST", result.Message ?? "Invalid request."))
        };
    }
}
