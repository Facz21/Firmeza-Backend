using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Features.Products.Commands.CreateProduct;
using EnterpriseSystem.Domain.Entities;
using EnterpriseSystem.Domain.Enums;
using EnterpriseSystem.Domain.Exceptions;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace EnterpriseSystem.Application.UnitTests.Products.Commands;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly CreateProductCommandValidator _validator;

    public CreateProductCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _validator = new CreateProductCommandValidator();
    }

    [Fact]
    public async Task Handle_ShouldAddProductAndCallSaveChanges_WhenCommandIsValid()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new Category { Id = categoryId, Name = "Test Category" };
        var categoriesList = new List<Category> { category };
        var productsList = new List<Product>();

        var mockCategoriesDbSet = categoriesList.BuildMockDbSet();
        var mockProductsDbSet = productsList.BuildMockDbSet();

        _mockContext.Setup(c => c.Categories).Returns(mockCategoriesDbSet.Object);
        _mockContext.Setup(c => c.Products).Returns(mockProductsDbSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreateProductCommandHandler(_mockContext.Object);
        var command = new CreateProductCommand
        {
            Sku = "TEST-SKU-001",
            Name = "Enterprise Test Product",
            Description = "Test Description",
            Price = 299.99m,
            StockQuantity = 10,
            Status = ProductStatus.Active,
            CategoryId = categoryId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        mockProductsDbSet.Verify(m => m.Add(It.Is<Product>(p =>
            p.Sku == "TEST-SKU-001" &&
            p.Name == "Enterprise Test Product" &&
            p.Price == 299.99m &&
            p.CategoryId == categoryId)), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Validator_ShouldFail_WhenPriceIsZeroOrNegative(decimal invalidPrice)
    {
        // Arrange
        var command = new CreateProductCommand
        {
            Sku = "SKU-100",
            Name = "Valid Name",
            Price = invalidPrice,
            StockQuantity = 5,
            CategoryId = Guid.NewGuid()
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.Price));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Validator_ShouldFail_WhenSkuIsEmpty(string? emptySku)
    {
        // Arrange
        var command = new CreateProductCommand
        {
            Sku = emptySku!,
            Name = "Valid Product",
            Price = 100m,
            StockQuantity = 10,
            CategoryId = Guid.NewGuid()
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductCommand.Sku));
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenSkuAlreadyExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var existingProduct = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "EXISTING-SKU",
            Name = "Existing",
            Price = 10m,
            CategoryId = categoryId
        };

        var productsList = new List<Product> { existingProduct };
        var categoriesList = new List<Category> { new() { Id = categoryId, Name = "Cat" } };

        _mockContext.Setup(c => c.Products).Returns(productsList.BuildMockDbSet().Object);
        _mockContext.Setup(c => c.Categories).Returns(categoriesList.BuildMockDbSet().Object);

        var handler = new CreateProductCommandHandler(_mockContext.Object);
        var command = new CreateProductCommand
        {
            Sku = "EXISTING-SKU",
            Name = "Duplicate Product",
            Price = 50m,
            CategoryId = categoryId
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Sku");
        exception.Which.Errors["Sku"].Should().Contain(e => e.Contains("already exists"));
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var nonExistentCategoryId = Guid.NewGuid();
        var productsList = new List<Product>();
        var categoriesList = new List<Category>();

        _mockContext.Setup(c => c.Products).Returns(productsList.BuildMockDbSet().Object);
        _mockContext.Setup(c => c.Categories).Returns(categoriesList.BuildMockDbSet().Object);

        var handler = new CreateProductCommandHandler(_mockContext.Object);
        var command = new CreateProductCommand
        {
            Sku = "NEW-SKU",
            Name = "Valid Name",
            Price = 100m,
            CategoryId = nonExistentCategoryId
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
