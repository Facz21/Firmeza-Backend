using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Features.Products.Commands.DeleteProduct;
using EnterpriseSystem.Application.Features.Products.Commands.UpdateProduct;
using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Enums;
using EnterpriseSystem.Domain.Exceptions;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace EnterpriseSystem.Application.UnitTests.Products.Commands;

public class UpdateAndDeleteProductCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;

    public UpdateAndDeleteProductCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
    }

    [Fact]
    public async Task UpdateProduct_ShouldUpdateFieldsAndSave_WhenValid()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var existingProduct = new Product
        {
            Id = productId,
            Sku = "OLD-SKU",
            Name = "Old Name",
            Price = 100m,
            StockQuantity = 5,
            Status = ProductStatus.Draft,
            CategoryId = categoryId
        };

        var productsList = new List<Product> { existingProduct };
        var categoriesList = new List<Category> { new() { Id = categoryId, Name = "Category A" } };

        _mockContext.Setup(c => c.Products).Returns(productsList.BuildMockDbSet().Object);
        _mockContext.Setup(c => c.Categories).Returns(categoriesList.BuildMockDbSet().Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductCommandHandler(_mockContext.Object);
        var command = new UpdateProductCommand
        {
            Id = productId,
            Sku = "NEW-SKU",
            Name = "Updated Name",
            Price = 199.99m,
            StockQuantity = 20,
            Status = ProductStatus.Active,
            CategoryId = categoryId
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        existingProduct.Sku.Should().Be("NEW-SKU");
        existingProduct.Name.Should().Be("Updated Name");
        existingProduct.Price.Should().Be(199.99m);
        existingProduct.StockQuantity.Should().Be(20);
        existingProduct.Status.Should().Be(ProductStatus.Active);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProduct_ShouldRemoveEntityAndSave_WhenProductExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingProduct = new Product
        {
            Id = productId,
            Sku = "DEL-SKU",
            Name = "To Delete",
            Price = 50m
        };

        var productsList = new List<Product> { existingProduct };
        var mockProductsDbSet = productsList.BuildMockDbSet();

        _mockContext.Setup(c => c.Products).Returns(mockProductsDbSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteProductCommandHandler(_mockContext.Object);

        // Act
        await handler.Handle(new DeleteProductCommand(productId), CancellationToken.None);

        // Assert
        mockProductsDbSet.Verify(m => m.Remove(It.Is<Product>(p => p.Id == productId)), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProduct_ShouldThrowNotFoundException_WhenProductDoesNotExist()
    {
        // Arrange
        var productsList = new List<Product>();
        _mockContext.Setup(c => c.Products).Returns(productsList.BuildMockDbSet().Object);

        var handler = new DeleteProductCommandHandler(_mockContext.Object);

        // Act
        var act = async () => await handler.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
