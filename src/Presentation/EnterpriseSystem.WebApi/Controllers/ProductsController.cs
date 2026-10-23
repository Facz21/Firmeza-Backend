using EnterpriseSystem.Application.Common.Models;
using EnterpriseSystem.Application.Features.Products.Commands.CreateProduct;
using EnterpriseSystem.Application.Features.Products.Commands.DeleteProduct;
using EnterpriseSystem.Application.Features.Products.Commands.ImportProductsExcel;
using EnterpriseSystem.Application.Features.Products.Commands.UpdateProduct;
using EnterpriseSystem.Application.Features.Products.DTOs;
using EnterpriseSystem.Application.Features.Products.Queries.ExportProductsExcel;
using EnterpriseSystem.Application.Features.Products.Queries.GetProductById;
using EnterpriseSystem.Application.Features.Products.Queries.GetProductPdfDetail;
using EnterpriseSystem.Application.Features.Products.Queries.GetProductsWithPagination;
using EnterpriseSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseSystem.WebApi.Controllers;

[Authorize]
public class ProductsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<ProductDto>>> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] ProductStatus? status = null)
    {
        var result = await Mediator.Send(new GetProductsWithPaginationQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            Status = status
        });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProductById(Guid id)
    {
        var result = await Mediator.Send(new GetProductByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Guid>> CreateProduct([FromBody] CreateProductCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetProductById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "Route ID and Body ID mismatch."
            });
        }

        await Mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        await Mediator.Send(new DeleteProductCommand(id));
        return NoContent();
    }

    [HttpGet("export/excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportToExcel(
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] ProductStatus? status = null)
    {
        var fileResult = await Mediator.Send(new ExportProductsExcelQuery
        {
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            Status = status
        });

        return File(fileResult.FileBytes, fileResult.ContentType, fileResult.FileName);
    }

    [HttpPost("import/excel")]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(typeof(ProductImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductImportResultDto>> ImportFromExcel(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid File",
                Detail = "Please provide a valid, non-empty Excel (.xlsx) file."
            });
        }

        using var stream = file.OpenReadStream();
        var result = await Mediator.Send(new ImportProductsExcelCommand(stream));

        return Ok(result);
    }

    [HttpGet("{id:guid}/export/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportToPdf(Guid id)
    {
        var fileResult = await Mediator.Send(new GetProductPdfDetailQuery(id));
        return File(fileResult.FileBytes, fileResult.ContentType, fileResult.FileName);
    }
}
