using EnterpriseSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseSystem.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Category> Categories { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
