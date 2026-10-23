using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Features.Products.DTOs;
using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseSystem.Application.Features.Products.Commands.ImportProductsExcel;

public record ImportProductsExcelCommand(Stream FileStream) : IRequest<ProductImportResultDto>;

public class ImportProductsExcelCommandHandler : IRequestHandler<ImportProductsExcelCommand, ProductImportResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IExcelService _excelService;

    public ImportProductsExcelCommandHandler(IApplicationDbContext context, IExcelService excelService)
    {
        _context = context;
        _excelService = excelService;
    }

    public async Task<ProductImportResultDto> Handle(ImportProductsExcelCommand request, CancellationToken cancellationToken)
    {
        var result = new ProductImportResultDto();
        var records = await _excelService.ImportFromExcelAsync<ProductExcelRecord>(request.FileStream, cancellationToken);

        if (records == null || records.Count == 0)
        {
            result.Errors.Add("The imported Excel file contains no valid data rows.");
            return result;
        }

        result.TotalProcessed = records.Count;

        // Cache existing categories to avoid redundant queries
        var categories = await _context.Categories.ToListAsync(cancellationToken);
        var existingProducts = await _context.Products.ToListAsync(cancellationToken);

        for (int i = 0; i < records.Count; i++)
        {
            var rowNumber = i + 2; // header is row 1
            var record = records[i];

            if (string.IsNullOrWhiteSpace(record.Sku))
            {
                result.Errors.Add($"Row {rowNumber}: SKU cannot be empty.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                result.Errors.Add($"Row {rowNumber}: Product name cannot be empty.");
                continue;
            }

            if (record.Price <= 0)
            {
                result.Errors.Add($"Row {rowNumber}: Price must be greater than zero.");
                continue;
            }

            if (record.StockQuantity < 0)
            {
                result.Errors.Add($"Row {rowNumber}: Stock quantity cannot be negative.");
                continue;
            }

            // Find or create category
            var categoryName = string.IsNullOrWhiteSpace(record.CategoryName) ? "General" : record.CategoryName.Trim();
            var category = categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (category == null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = categoryName,
                    Description = $"Auto-created during Excel import"
                };
                _context.Categories.Add(category);
                categories.Add(category);
            }

            // Parse status
            var status = ProductStatus.Active;
            if (Enum.TryParse<ProductStatus>(record.Status, true, out var parsedStatus))
            {
                status = parsedStatus;
            }

            var normalizedSku = record.Sku.Trim().ToUpperInvariant();
            var existingProduct = existingProducts.FirstOrDefault(p => p.Sku.Equals(normalizedSku, StringComparison.OrdinalIgnoreCase));

            if (existingProduct != null)
            {
                existingProduct.Name = record.Name.Trim();
                existingProduct.Description = record.Description?.Trim();
                existingProduct.Price = record.Price;
                existingProduct.StockQuantity = record.StockQuantity;
                existingProduct.Status = status;
                existingProduct.CategoryId = category.Id;
                result.Updated++;
            }
            else
            {
                var newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Sku = normalizedSku,
                    Name = record.Name.Trim(),
                    Description = record.Description?.Trim(),
                    Price = record.Price,
                    StockQuantity = record.StockQuantity,
                    Status = status,
                    CategoryId = category.Id
                };
                _context.Products.Add(newProduct);
                existingProducts.Add(newProduct);
                result.Inserted++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return result;
    }
}
