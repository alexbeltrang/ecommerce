using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Catalog;
using MercadoCercano.Application.Promotions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController, Authorize(Policy = RolePolicies.Administrator), Route("api/v1/admin/promotions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPromotionsController(IPromotionService service) : ControllerBase
{
    /// <summary>Lista promociones y cupones, incluidos inactivos y agotados.</summary>
    [HttpGet]
    public async Task<ActionResult<PageResponse<PromotionResponse>>> List([FromQuery] PromotionQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));
    /// <summary>Obtiene la regla actual y su version administrativa.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PromotionResponse>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    /// <summary>Crea una promocion automatica o un cupon con codigo unico.</summary>
    [HttpPost, ProducesResponseType(typeof(PromotionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PromotionResponse>> Create(PromotionWriteRequest request, CancellationToken ct)
    {
        var result = await service.SaveAsync(null, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    /// <summary>Edita o desactiva sin borrar pedidos ni usos historicos.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PromotionResponse>> Update(Guid id, PromotionWriteRequest request, CancellationToken ct) =>
        Ok(await service.SaveAsync(id, request, UserId, ct));
    /// <summary>Lista usos consumidos y liberados sin datos personales del comprador.</summary>
    [HttpGet("{id:guid}/usages")]
    public async Task<ActionResult<PageResponse<PromotionUsageResponse>>> Usages(Guid id, [FromQuery, Range(1, int.MaxValue)] int page = 1, CancellationToken ct = default) =>
        Ok(await service.UsagesAsync(id, page, ct));
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
