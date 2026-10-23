using EnterpriseSystem.Application.Features.Products.DTOs;

namespace EnterpriseSystem.Application.Common.Interfaces;

public interface IPdfService
{
    Task<byte[]> GenerateProductDetailPdfAsync(ProductDto product, CancellationToken cancellationToken = default);
}
