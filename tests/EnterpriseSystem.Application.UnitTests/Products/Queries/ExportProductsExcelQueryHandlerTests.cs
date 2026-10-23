using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Features.Products.DTOs;
using EnterpriseSystem.Application.Features.Products.Queries.ExportProductsExcel;
using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Enums;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace EnterpriseSystem.Application.UnitTests.Products.Queries;

public class ExportProductsExcelQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IExcelService> _mockExcelService;

    public ExportProductsExcelQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockExcelService = new Mock<IExcelService>();
    }

    [Fact]
    public async Task Handle_ShouldInvokeExcelServiceWithExpectedData()
    {
        // Arrange
        var category = new Category { Id = Guid.NewGuid(), Name = "Datacenter Hardware" };
        var products = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Sku = "SRV-001",
                Name = "Rack Server A",
                Description = "High perf node",
                Price = 5000.00m,
                StockQuantity = 10,
                Status = ProductStatus.Active,
                Category = category,
                CategoryId = category.Id
            },
            new()
            {
                Id = Guid.NewGuid(),
                Sku = "SRV-002",
                Name = "Rack Server B",
                Description = "Storage node",
                Price = 3500.00m,
                StockQuantity = 5,
                Status = ProductStatus.Active,
                Category = category,
                CategoryId = category.Id
            }
        };

        var mockProductsDbSet = products.BuildMockDbSet();
        _mockContext.Setup(c => c.Products).Returns(mockProductsDbSet.Object);

        var fakeExcelBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // zip/xlsx magic numbers
        _mockExcelService.Setup(e => e.ExportToExcelAsync(
                It.IsAny<IEnumerable<ProductExcelRecord>>(),
                "Products",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeExcelBytes);

        var handler = new ExportProductsExcelQueryHandler(_mockContext.Object, _mockExcelService.Object);
        var query = new ExportProductsExcelQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileBytes.Should().Equal(fakeExcelBytes);
        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.FileName.Should().StartWith("Products_Export_").And.EndWith(".xlsx");

        _mockExcelService.Verify(e => e.ExportToExcelAsync(
            It.Is<IEnumerable<ProductExcelRecord>>(list =>
                list.Count() == 2 &&
                list.Any(p => p.Sku == "SRV-001" && p.Price == 5000.00m) &&
                list.Any(p => p.Sku == "SRV-002" && p.Price == 3500.00m)),
            "Products",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
