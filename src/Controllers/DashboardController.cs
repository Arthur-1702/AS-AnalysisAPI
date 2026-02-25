using AnalysisService.Models;
using AnalysisService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnalysisService.Controllers;

[ApiController]
[Route("dashboard")]
[Authorize]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// Retorna o dashboard consolidado para uma lista de talhões.
    /// Inclui status atual (Normal/Alerta de Seca/Risco de Praga/Risco de Alagamento)
    /// e todos os alertas ativos.
    /// </summary>
    /// <param name="fieldIds">Lista de IDs de talhões (query string: ?fieldIds=id1&amp;fieldIds=id2)</param>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] List<Guid> fieldIds,
        CancellationToken ct)
    {
        if (fieldIds.Count == 0)
            return BadRequest(new { error = "Informe ao menos um fieldId." });

        var result = await dashboardService.GetDashboardAsync(fieldIds, ct);
        return Ok(result);
    }

    /// <summary>Verifica saúde do serviço.</summary>
    [AllowAnonymous]
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy", service = "AnalysisService" });
}
