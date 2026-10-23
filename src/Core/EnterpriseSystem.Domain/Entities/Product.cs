using EnterpriseSystem.Domain.Common;
using EnterpriseSystem.Domain.Enums;

namespace EnterpriseSystem.Domain.Entities;

public class Product : AuditableEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
}
