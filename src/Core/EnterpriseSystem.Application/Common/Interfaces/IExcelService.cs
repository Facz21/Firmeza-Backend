namespace EnterpriseSystem.Application.Common.Interfaces;

public interface IExcelService
{
    Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName = "Data", CancellationToken cancellationToken = default);
    Task<List<T>> ImportFromExcelAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : new();
}
