using AnalysisService.Models;
using AnalysisService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnalysisService.Controllers;

[ApiController]
[Route("alerts")]
[Authorize]
public class AlertsController(DashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// Retorna alertas de um talhão.
    /// </summary>
    [HttpGet("{fieldId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AlertResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts(
        Guid fieldId,
        [FromQuery] bool onlyActive = true,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await dashboardService.GetAlertsAsync(fieldId, onlyActive, limit, ct);
        return Ok(result);
    }

    /// <summary>
    /// Marca um alerta como resolvido.
    /// </summary>
    [HttpPatch("{alertId:guid}/resolve")]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(Guid alertId, CancellationToken ct)
    {
        var result = await dashboardService.ResolveAlertAsync(alertId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
