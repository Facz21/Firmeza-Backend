using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseSystem.WebApi.Controllers;

public record CreateOrderItemRequest(Guid ProductId, int Quantity);

public record CreatePurchaseOrderRequest(
    string DeliveryPoint,
    PaymentMethod PaymentMethod,
    string? Notes,
    List<CreateOrderItemRequest> Items);

public record ReviewOrderRequest(string? AdminComments);

public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string DeliveryPoint { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? AdminComments { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}

[Authorize]
public class OrdersController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    public OrdersController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(List<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PurchaseOrderDto>>> GetMyOrders(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var userId = _currentUserService.UserId.Value;
        var orders = await _context.PurchaseOrders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(MapToDto).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Auditor}")]
    [ProducesResponseType(typeof(List<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PurchaseOrderDto>>> GetAllOrders(
        [FromQuery] PurchaseOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseOrders
            .Include(o => o.Items)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(MapToDto).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PurchaseOrderDto>> SubmitOrder(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DeliveryPoint))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Delivery Point",
                Detail = "Debes especificar el punto de entrega para la compra."
            });
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Empty Cart",
                Detail = "El carrito debe contener al menos un producto."
            });
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var orderItems = new List<PurchaseOrderItem>();
        decimal totalAmount = 0m;

        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Quantity",
                    Detail = "La cantidad de cada producto debe ser mayor a 0."
                });
            }

            if (!products.TryGetValue(itemReq.ProductId, out var product))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Product Not Found",
                    Detail = $"El producto con ID {itemReq.ProductId} no existe."
                });
            }

            if (product.Status != ProductStatus.Active)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Product Unavailable",
                    Detail = $"El producto '{product.Name}' ({product.Sku}) no se encuentra activo."
                });
            }

            if (product.StockQuantity < itemReq.Quantity)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Insufficient Stock",
                    Detail = $"Stock insuficiente para '{product.Name}' ({product.Sku}). Disponible: {product.StockQuantity}, Solicitado: {itemReq.Quantity}."
                });
            }

            var lineTotal = product.Price * itemReq.Quantity;
            totalAmount += lineTotal;

            orderItems.Add(new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductSku = product.Sku,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = itemReq.Quantity,
                LineTotal = lineTotal
            });
        }

        var userId = _currentUserService.UserId.Value;
        var userEmail = _currentUserService.Email ?? "operator@enterprise.com";
        var fullName = await _identityService.GetUserNameAsync(userId, cancellationToken) ?? userEmail;

        var randomSuffix = Random.Shared.Next(1000, 9999);
        var orderNumber = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{randomSuffix}";

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            UserId = userId,
            UserEmail = userEmail,
            UserFullName = fullName,
            DeliveryPoint = request.DeliveryPoint.Trim(),
            PaymentMethod = request.PaymentMethod,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = PurchaseOrderStatus.PendingApproval,
            TotalAmount = totalAmount,
            Items = orderItems
        };

        _context.PurchaseOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetMyOrders), new { id = order.Id }, MapToDto(order));
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDto>> ApproveOrder(
        Guid id,
        [FromBody] ReviewOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
        {
            return NotFound();
        }

        if (order.Status != PurchaseOrderStatus.PendingApproval)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Order Already Processed",
                Detail = $"El carrito {order.OrderNumber} ya fue procesado ({order.Status})."
            });
        }

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Product Missing",
                    Detail = $"El producto {item.ProductSku} ya no existe en el inventario."
                });
            }

            if (product.StockQuantity < item.Quantity)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Insufficient Stock for Approval",
                    Detail = $"No hay stock suficiente para aprobar '{product.Name}' ({product.Sku}). Disponible: {product.StockQuantity}, Solicitado: {item.Quantity}."
                });
            }
        }

        // Deduct stock for all items in the approved cart
        foreach (var item in order.Items)
        {
            var product = products[item.ProductId];
            product.StockQuantity -= item.Quantity;
            if (product.StockQuantity == 0 && product.Status == ProductStatus.Active)
            {
                product.Status = ProductStatus.OutOfStock;
            }
        }

        order.Status = PurchaseOrderStatus.Approved;
        order.ReviewedAt = DateTimeOffset.UtcNow;
        order.ReviewedBy = _currentUserService.Email ?? "Administrator";
        order.AdminComments = string.IsNullOrWhiteSpace(request?.AdminComments)
            ? "Compra aprobada y stock descontado del inventario."
            : request.AdminComments.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToDto(order));
    }

    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = Roles.Administrator)]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDto>> RejectOrder(
        Guid id,
        [FromBody] ReviewOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
        {
            return NotFound();
        }

        if (order.Status != PurchaseOrderStatus.PendingApproval)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Order Already Processed",
                Detail = $"El carrito {order.OrderNumber} ya fue procesado ({order.Status})."
            });
        }

        order.Status = PurchaseOrderStatus.Rejected;
        order.ReviewedAt = DateTimeOffset.UtcNow;
        order.ReviewedBy = _currentUserService.Email ?? "Administrator";
        order.AdminComments = string.IsNullOrWhiteSpace(request?.AdminComments)
            ? "Solicitud de compra rechazada por administración."
            : request.AdminComments.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToDto(order));
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder order)
    {
        return new PurchaseOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            UserEmail = order.UserEmail,
            UserFullName = order.UserFullName,
            DeliveryPoint = order.DeliveryPoint,
            PaymentMethod = order.PaymentMethod,
            Notes = order.Notes,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            ReviewedAt = order.ReviewedAt,
            ReviewedBy = order.ReviewedBy,
            AdminComments = order.AdminComments,
            Items = order.Items.Select(i => new PurchaseOrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductSku = i.ProductSku,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = i.LineTotal
            }).ToList()
        };
    }
}
