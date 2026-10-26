using System.Security.Claims;
using MercadoCercano.Api.Authorization;
using MercadoCercano.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoCercano.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.Administrator)]
[Route("api/v1/admin/categories")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCategoriesController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Lista categorias activas e inactivas para administracion.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(CancellationToken ct) =>
        Ok(await catalog.CategoriesAsync(true, ct));

    /// <summary>Obtiene una categoria para editar.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await catalog.CategoryAsync(id, ct));

    /// <summary>Crea una categoria con URL unica.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryWriteRequest request, CancellationToken ct)
    {
        var result = await catalog.SaveCategoryAsync(null, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Actualiza datos, orden y estado de una categoria sin borrar su historial.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Update(Guid id, CategoryWriteRequest request, CancellationToken ct) =>
        Ok(await catalog.SaveCategoryAsync(id, request, UserId, ct));

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
