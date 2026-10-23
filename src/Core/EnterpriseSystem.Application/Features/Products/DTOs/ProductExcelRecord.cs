namespace EnterpriseSystem.Application.Features.Products.DTOs;

public class ProductExcelRecord
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Status { get; set; } = "Active";
    public string CategoryName { get; set; } = string.Empty;
}
