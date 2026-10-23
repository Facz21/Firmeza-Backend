using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Common.Models;
using EnterpriseSystem.Application.Features.Products.DTOs;
using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseSystem.Application.Features.Products.Queries.GetProductPdfDetail;

public record GetProductPdfDetailQuery(Guid Id) : IRequest<FileExportResult>;

public class GetProductPdfDetailQueryHandler : IRequestHandler<GetProductPdfDetailQuery, FileExportResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfService _pdfService;

    public GetProductPdfDetailQueryHandler(IApplicationDbContext context, IPdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    public async Task<FileExportResult> Handle(GetProductPdfDetailQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), request.Id);
        }

        var dto = new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            Status = product.Status,
            CategoryId = product.CategoryId,
            CategoryName = product.Category != null ? product.Category.Name : "N/A",
            CreatedAt = product.CreatedAt,
            CreatedBy = product.CreatedBy,
            LastModifiedAt = product.LastModifiedAt,
            LastModifiedBy = product.LastModifiedBy
        };

        var bytes = await _pdfService.GenerateProductDetailPdfAsync(dto, cancellationToken);
        var fileName = $"Product_{dto.Sku}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.pdf";
        var contentType = "application/pdf";

        return new FileExportResult(bytes, contentType, fileName);
    }
}
