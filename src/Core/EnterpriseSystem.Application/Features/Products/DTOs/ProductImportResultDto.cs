namespace EnterpriseSystem.Application.Features.Products.DTOs;

public class ProductImportResultDto
{
    public int TotalProcessed { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public List<string> Errors { get; set; } = new();
}
