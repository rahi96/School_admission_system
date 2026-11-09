using Microsoft.AspNetCore.Mvc;
using UserApi.Models;
using UserApi.Services;

namespace UserApi.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly AdmissionService _service;

    public ClassesController(AdmissionService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClassResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest? request)
    {
        if (request is null)
            return BadRequest(new ErrorResponse("BAD_REQUEST", "Request body is required."));

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ErrorResponse("BAD_REQUEST", "name is required."));

        if (request.TotalSeats is null or <= 0)
            return BadRequest(new ErrorResponse("BAD_REQUEST", "totalSeats must be a positive integer."));

        var created = await _service.CreateClassAsync(request.Name.Trim(), request.TotalSeats.Value);
        return CreatedAtAction(nameof(GetById), new { classId = created.Id }, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClassStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var classes = await _service.ListClassesAsync();
        return Ok(classes);
    }

    [HttpGet("{classId}")]
    [ProducesResponseType(typeof(ClassStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string classId)
    {
        var result = await _service.GetClassAsync(classId);
        if (result is null)
            return NotFound(new ErrorResponse("NOT_FOUND", "Class not found."));
        return Ok(result);
    }

    [HttpPost("{classId}/holds")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHold(string classId, [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        var validation = StudentIdValidation(studentId);
        if (validation is not null) return validation;

        var result = await _service.CreateHoldAsync(classId, studentId!);
        return result.Status switch
        {
            ResultStatus.Ok => StatusCode(StatusCodes.Status201Created, result.Value),
            ResultStatus.NotFound => NotFound(new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Conflict => Conflict(new ErrorResponse(result.Error!, result.Message!)),
            ResultStatus.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse(result.Error!, result.Message!)),
            _ => BadRequest(new ErrorResponse(result.Error ?? "BAD_REQUEST", result.Message ?? "Invalid request."))
        };
    }

    private IActionResult? StudentIdValidation(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(new ErrorResponse("BAD_REQUEST", "X-Student-Id header is required."));
        return null;
    }
}
