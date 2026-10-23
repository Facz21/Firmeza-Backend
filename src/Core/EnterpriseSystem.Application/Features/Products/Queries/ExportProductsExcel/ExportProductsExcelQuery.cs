using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Common.Models;
using EnterpriseSystem.Application.Features.Products.DTOs;
using EnterpriseSystem.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseSystem.Application.Features.Products.Queries.ExportProductsExcel;

public record ExportProductsExcelQuery : IRequest<FileExportResult>
{
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public ProductStatus? Status { get; init; }
}

public class ExportProductsExcelQueryHandler : IRequestHandler<ExportProductsExcelQuery, FileExportResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IExcelService _excelService;

    public ExportProductsExcelQueryHandler(IApplicationDbContext context, IExcelService excelService)
    {
        _context = context;
        _excelService = excelService;
    }

    public async Task<FileExportResult> Handle(ExportProductsExcelQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Sku.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)));
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }

        var products = await query
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var records = products.Select(p => new ProductExcelRecord
        {
            Sku = p.Sku,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            Status = p.Status.ToString(),
            CategoryName = p.Category != null ? p.Category.Name : string.Empty
        }).ToList();

        var bytes = await _excelService.ExportToExcelAsync(records, "Products", cancellationToken);
        var fileName = $"Products_Export_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx";
        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return new FileExportResult(bytes, contentType, fileName);
    }
}
