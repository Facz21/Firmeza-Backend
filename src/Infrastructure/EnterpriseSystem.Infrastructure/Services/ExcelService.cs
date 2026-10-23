using System.Drawing;
using System.Reflection;
using EnterpriseSystem.Application.Common.Interfaces;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace EnterpriseSystem.Infrastructure.Services;

public class ExcelService : IExcelService
{
    static ExcelService()
    {
        try
        {
#pragma warning disable CS0618
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
#pragma warning restore CS0618
        }
        catch
        {
            // Handled for EPPlus 8+
        }

        try
        {
            ExcelPackage.License.SetNonCommercialPersonal("EnterpriseSystem");
        }
        catch
        {
            // Fallback for earlier versions
        }
    }

    public Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName = "Data", CancellationToken cancellationToken = default)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        // 1. Write Header Row
        for (int col = 0; col < properties.Length; col++)
        {
            var cell = worksheet.Cells[1, col + 1];
            cell.Value = SplitCamelCase(properties[col].Name);
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 41, 59)); // Slate 800
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(71, 85, 105));
        }

        worksheet.Row(1).Height = 28;

        // 2. Write Data Rows
        int rowIndex = 2;
        foreach (var item in data)
        {
            for (int col = 0; col < properties.Length; col++)
            {
                var cell = worksheet.Cells[rowIndex, col + 1];
                var prop = properties[col];
                var value = prop.GetValue(item);

                if (value != null)
                {
                    if (prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(decimal?))
                    {
                        cell.Value = value;
                        cell.Style.Numberformat.Format = "$#,##0.00";
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }
                    else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?) ||
                             prop.PropertyType == typeof(long) || prop.PropertyType == typeof(long?))
                    {
                        cell.Value = value;
                        cell.Style.Numberformat.Format = "#,##0";
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }
                    else if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTimeOffset))
                    {
                        cell.Value = value.ToString();
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }
                    else
                    {
                        cell.Value = value.ToString();
                    }
                }

                // Zebra striping
                if (rowIndex % 2 == 1)
                {
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 250, 252)); // Slate 50
                }

                cell.Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.FromArgb(226, 232, 240));
            }
            worksheet.Row(rowIndex).Height = 22;
            rowIndex++;
        }

        worksheet.Cells[worksheet.Dimension?.Address ?? "A1"].AutoFitColumns(12, 50);

        return Task.FromResult(package.GetAsByteArray());
    }

    public Task<List<T>> ImportFromExcelAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : new()
    {
        var resultList = new List<T>();
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();

        if (worksheet == null || worksheet.Dimension == null)
        {
            return Task.FromResult(resultList);
        }

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        var columnMapping = new Dictionary<int, PropertyInfo>();
        int colCount = worksheet.Dimension.Columns;
        int rowCount = worksheet.Dimension.Rows;

        // Map header columns to properties
        for (int col = 1; col <= colCount; col++)
        {
            var headerValue = worksheet.Cells[1, col].Text?.Trim();
            if (string.IsNullOrWhiteSpace(headerValue)) continue;

            var normalizedHeader = NormalizeName(headerValue);
            var prop = properties.FirstOrDefault(p =>
                NormalizeName(p.Name).Equals(normalizedHeader, StringComparison.OrdinalIgnoreCase));

            if (prop != null)
            {
                columnMapping[col] = prop;
            }
        }

        // Read data rows
        for (int row = 2; row <= rowCount; row++)
        {
            var instance = new T();
            bool hasAnyValue = false;

            foreach (var kvp in columnMapping)
            {
                int col = kvp.Key;
                var prop = kvp.Value;
                var rawValue = worksheet.Cells[row, col].Text?.Trim();

                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    hasAnyValue = true;
                    try
                    {
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                        if (targetType == typeof(decimal))
                        {
                            var cleaned = rawValue.Replace("$", "").Replace(",", "").Trim();
                            if (decimal.TryParse(cleaned, System.Globalization.CultureInfo.InvariantCulture, out var decVal))
                                prop.SetValue(instance, decVal);
                        }
                        else if (targetType == typeof(int))
                        {
                            var cleaned = rawValue.Replace(",", "").Trim();
                            if (int.TryParse(cleaned, out var intVal))
                                prop.SetValue(instance, intVal);
                        }
                        else if (targetType == typeof(Guid))
                        {
                            if (Guid.TryParse(rawValue, out var guidVal))
                                prop.SetValue(instance, guidVal);
                        }
                        else if (targetType.IsEnum)
                        {
                            if (Enum.TryParse(targetType, rawValue, true, out var enumVal))
                                prop.SetValue(instance, enumVal);
                        }
                        else
                        {
                            var converted = Convert.ChangeType(rawValue, targetType);
                            prop.SetValue(instance, converted);
                        }
                    }
                    catch
                    {
                        // Ignore conversion failure on optional field
                    }
                }
            }

            if (hasAnyValue)
            {
                resultList.Add(instance);
            }
        }

        return Task.FromResult(resultList);
    }

    private static string NormalizeName(string name)
    {
        return new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string SplitCamelCase(string input)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
    }
}
