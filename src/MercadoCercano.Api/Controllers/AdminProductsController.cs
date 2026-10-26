using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.Administrator)]
[Route("api/v1/admin/products")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminProductsController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Lista productos, incluidos inactivos, con costos e inventario.</summary>
    [HttpGet]
    public async Task<ActionResult<PageResponse<AdminProductResponse>>> List([FromQuery] CatalogQuery query, CancellationToken ct) =>
        Ok(await catalog.AdminProductsAsync(query, ct));

    /// <summary>Obtiene todos los datos administrativos de un producto.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminProductResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await catalog.AdminProductAsync(id, ct));

    /// <summary>Crea un producto y registra su inventario inicial.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminProductResponse>> Create(ProductWriteRequest request, CancellationToken ct)
    {
        var result = await catalog.SaveProductAsync(null, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Product.Id }, result);
    }

    /// <summary>Actualiza producto, estado y existencias con motivo y control de version.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminProductResponse>> Update(Guid id, ProductWriteRequest request, CancellationToken ct) =>
        Ok(await catalog.SaveProductAsync(id, request, UserId, ct));

    /// <summary>Asocia una imagen PNG, JPEG o WebP de hasta 750 KiB; maximo 8 por producto.</summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1_048_576)]
    [ProducesResponseType(typeof(ProductImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductImageResponse>> Upload(
        Guid id, [FromForm] IFormFile file, [FromForm, StringLength(300)] string? altText, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await catalog.UploadAsync(id, stream, file.Length, file.ContentType, altText, UserId, ct);
        return Created(result.ImageUrl, result);
    }

    /// <summary>Cambia el texto alternativo y el orden de una imagen del producto.</summary>
    [HttpPut("{id:guid}/images/{imageId:guid}")]
    public async Task<ActionResult<ProductImageResponse>> UpdateImage(
        Guid id, Guid imageId, ImageWriteRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdateImageAsync(id, imageId, request, UserId, ct));

    /// <summary>Elimina una imagen del producto y su archivo local.</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken ct)
    {
        await catalog.DeleteImageAsync(id, imageId, UserId, ct);
        return NoContent();
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
