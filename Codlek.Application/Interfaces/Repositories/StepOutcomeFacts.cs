namespace Codlek.Application.Interfaces.Repositories;

/// <summary>نتيجة مرحلة واحدة في فحص واحد — خام.</summary>
public sealed record StepOutcomeFacts(Guid ReportId, string Title, int Status);
