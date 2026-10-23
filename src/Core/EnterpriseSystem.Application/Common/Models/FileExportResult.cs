namespace EnterpriseSystem.Application.Common.Models;

public record FileExportResult(byte[] FileBytes, string ContentType, string FileName);
