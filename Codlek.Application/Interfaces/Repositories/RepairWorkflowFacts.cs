using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// حركة من السجل التشغيلي — <b>بأرقامها الخام</b>.
///
/// <para>⚠️ نوع الحركة بيرجع كـenum مش كنص؛ الترجمة للعربي بتحصل
/// في الـHandler.</para>
/// </summary>
public sealed record RepairWorkflowFacts(
    DeviceWorkflowEventType EventType,
    string ActorName,
    DateTime OccurredAtUtc,
    string Reason);
