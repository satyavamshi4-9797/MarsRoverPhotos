using MarsRoverPhotos.Models;
using MarsRoverPhotos.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarsRoverPhotos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoverPhotosController : ControllerBase
{
    private readonly IRoverPhotoOrchestrator _orchestrator;
    private readonly ILogger<RoverPhotosController> _logger;

    public RoverPhotosController(IRoverPhotoOrchestrator orchestrator, ILogger<RoverPhotosController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Processes the dates.txt file, downloads Mars rover photos, and returns a summary.
    /// </summary>
    /// <param name="filePath">Optional custom path to the dates file. Defaults to dates.txt in current directory.</param>
    [HttpGet("process")]
    [ProducesResponseType(typeof(SummaryReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SummaryReport>> ProcessDates(
        [FromQuery] string? filePath = null,
        CancellationToken ct = default)
    {
        var path = filePath ?? Path.Combine(Directory.GetCurrentDirectory(), "dates.txt");

        try
        {
            var report = await _orchestrator.ProcessDatesFileAsync(path, ct);
            return Ok(report);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Dates file not found at {Path}", path);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing dates");
            return StatusCode(500, new { error = "An unexpected error occurred", detail = ex.Message });
        }
    }
}
