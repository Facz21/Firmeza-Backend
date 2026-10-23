using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Features.Products.DTOs;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace EnterpriseSystem.Infrastructure.Services;

public class PdfService : IPdfService
{
    public Task<byte[]> GenerateProductDetailPdfAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        var writer = new PdfWriter(memoryStream);
        var pdfDocument = new PdfDocument(writer);
        var document = new Document(pdfDocument, PageSize.A4);
        document.SetMargins(40, 40, 40, 40);

        var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

        var primaryColor = new DeviceRgb(30, 41, 59);    // #1E293B (Slate 800)
        var accentColor = new DeviceRgb(37, 99, 235);    // #2563EB (Blue 600)
        var subtleGray = new DeviceRgb(241, 245, 249);   // #F1F5F9 (Slate 100)
        var textMuted = new DeviceRgb(100, 116, 139);    // #64748B (Slate 500)
        var borderGray = new DeviceRgb(226, 232, 240);   // #E2E8F0 (Slate 200)

        // 1. Header Banner Table (Two Columns)
        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 70, 30 }))
            .UseAllAvailableWidth()
            .SetMarginBottom(20);

        var headerLeft = new Cell()
            .SetBorder(Border.NO_BORDER)
            .Add(new Paragraph("ENTERPRISE INVENTORY MANAGEMENT")
                .SetFont(boldFont)
                .SetFontSize(10)
                .SetFontColor(accentColor)
                .SetMarginBottom(2))
            .Add(new Paragraph("Product Specification Report")
                .SetFont(boldFont)
                .SetFontSize(20)
                .SetFontColor(primaryColor)
                .SetMarginBottom(4))
            .Add(new Paragraph($"Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                .SetFont(regularFont)
                .SetFontSize(9)
                .SetFontColor(textMuted));

        var statusText = product.Status.ToString().ToUpper();
        var headerRight = new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetTextAlignment(TextAlignment.RIGHT)
            .Add(new Paragraph(statusText)
                .SetFont(boldFont)
                .SetFontSize(11)
                .SetFontColor(ColorConstants.WHITE)
                .SetBackgroundColor(accentColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(6)
                .SetMarginTop(10));

        headerTable.AddCell(headerLeft);
        headerTable.AddCell(headerRight);
        document.Add(headerTable);

        // Divider
        var divider = new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f))
            .SetStrokeColor(borderGray)
            .SetMarginBottom(20);
        document.Add(divider);

        // 2. Specifications Table
        var specTitle = new Paragraph("Product Specifications")
            .SetFont(boldFont)
            .SetFontSize(14)
            .SetFontColor(primaryColor)
            .SetMarginBottom(10);
        document.Add(specTitle);

        var table = new Table(UnitValue.CreatePercentArray(new float[] { 30, 70 }))
            .UseAllAvailableWidth()
            .SetMarginBottom(20);

        void AddRow(string label, string? value, bool isEven)
        {
            var bg = isEven ? subtleGray : ColorConstants.WHITE;

            var labelCell = new Cell()
                .SetBackgroundColor(bg)
                .SetPadding(8)
                .SetBorder(new SolidBorder(borderGray, 0.5f))
                .Add(new Paragraph(label)
                    .SetFont(boldFont)
                    .SetFontSize(10)
                    .SetFontColor(primaryColor));

            var valueCell = new Cell()
                .SetBackgroundColor(bg)
                .SetPadding(8)
                .SetBorder(new SolidBorder(borderGray, 0.5f))
                .Add(new Paragraph(value ?? "N/A")
                    .SetFont(regularFont)
                    .SetFontSize(10)
                    .SetFontColor(primaryColor));

            table.AddCell(labelCell);
            table.AddCell(valueCell);
        }

        AddRow("Product ID", product.Id.ToString(), false);
        AddRow("SKU Code", product.Sku, true);
        AddRow("Product Name", product.Name, false);
        AddRow("Category", product.CategoryName, true);
        AddRow("Price", $"${product.Price:F2} USD", false);
        AddRow("Stock Quantity", $"{product.StockQuantity} units", true);
        AddRow("Lifecycle Status", product.Status.ToString(), false);
        AddRow("Creation Date", product.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), true);
        AddRow("Created By", product.CreatedBy, false);
        AddRow("Last Modified", product.LastModifiedAt.HasValue ? product.LastModifiedAt.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") : "Never modified", true);
        AddRow("Modified By", product.LastModifiedBy, false);

        document.Add(table);

        // 3. Description Block
        var descTitle = new Paragraph("Description")
            .SetFont(boldFont)
            .SetFontSize(12)
            .SetFontColor(primaryColor)
            .SetMarginBottom(6);
        document.Add(descTitle);

        var descText = string.IsNullOrWhiteSpace(product.Description)
            ? "No detailed description provided for this product."
            : product.Description;

        var descBox = new Div()
            .SetBackgroundColor(subtleGray)
            .SetBorder(new SolidBorder(borderGray, 1f))
            .SetPadding(12)
            .SetMarginBottom(30)
            .Add(new Paragraph(descText)
                .SetFont(regularFont)
                .SetFontSize(10)
                .SetFontColor(primaryColor));
        document.Add(descBox);

        // 4. Footer Note
        var footer = new Paragraph("CONFIDENTIAL - AUTOMATICALLY GENERATED BY ENTERPRISE ASSET SYSTEM - ALL RIGHTS RESERVED")
            .SetFont(regularFont)
            .SetFontSize(8)
            .SetFontColor(textMuted)
            .SetTextAlignment(TextAlignment.CENTER);
        document.Add(footer);

        document.Close();
        return Task.FromResult(memoryStream.ToArray());
    }
}
