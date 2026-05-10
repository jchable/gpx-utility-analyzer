namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/nutrition-products")]
public class NutritionProductsController : ControllerBase
{
    private readonly NutritionProductService _service;

    public NutritionProductsController(NutritionProductService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<NutritionProductDto>>> GetProducts(
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var products = await _service.ListAsync(userId, type, ct);
        return products;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NutritionProductDto>> GetProduct(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var product = await _service.GetAsync(userId, id, ct);
        if (product is null) return NotFound();
        return product;
    }

    [HttpPost]
    public async Task<ActionResult<NutritionProductDto>> CreateProduct(
        [FromBody] NutritionProductCreateDto dto, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var product = await _service.CreateAsync(userId, dto, ct);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NutritionProductDto>> UpdateProduct(
        Guid id, [FromBody] NutritionProductUpdateDto dto, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var product = await _service.UpdateAsync(userId, id, dto, ct);
        if (product is null) return NotFound();
        return product;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var success = await _service.DeleteAsync(userId, id, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("import-defaults")]
    public async Task<IActionResult> ImportDefaults(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        int count = await _service.ImportDefaultsAsync(userId, ct);
        return Ok(new { imported = count });
    }
}
