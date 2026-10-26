using MercadoCercano.Application.Catalog;
using MercadoCercano.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/catalog")]
public sealed class CatalogController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Lista las categorias activas en su orden de presentacion.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> Categories(CancellationToken ct) =>
        Ok(await catalog.CategoriesAsync(false, ct));

    /// <summary>Busca productos activos de categorias activas con filtros y paginacion.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<PageResponse<ProductResponse>>> Products([FromQuery] CatalogQuery query, CancellationToken ct) =>
        Ok(await catalog.ProductsAsync(query, ct));

    /// <summary>Obtiene el detalle publico de un producto por su URL.</summary>
    [HttpGet("products/{slug}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Product(string slug, CancellationToken ct) =>
        Ok(await catalog.ProductAsync(slug, ct));

    /// <summary>Revalida un producto del carrito aunque su URL haya cambiado.</summary>
    [HttpGet("products/by-id/{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> ProductById(Guid id, CancellationToken ct) =>
        Ok(await catalog.PublicProductAsync(id, ct));

    /// <summary>Sirve una imagen asociada a un producto visible o accesible al administrador.</summary>
    [HttpGet("images/{fileName}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Image(string fileName, CancellationToken ct)
    {
        var file = await catalog.OpenImageAsync(fileName, User.IsInRole(nameof(UserRole.Administrator)), ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        return File(file.Stream, file.ContentType);
    }
}
