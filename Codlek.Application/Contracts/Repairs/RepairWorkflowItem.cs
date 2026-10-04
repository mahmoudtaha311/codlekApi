namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// حركة في السجل التشغيلي متعلّقة بأمر الصيانة.
///
/// <para>🔴 <b><c>FromStageText</c> و<c>ToStageText</c> بيرجعوا
/// فاضيين النهاردة.</b> المشروع القديم بيحطّهم <c>""</c> في الإسقاط
/// ومابيعبّيهمش بعد كده أبداً. سيبهم فاضيين، أو عبّيهم بقرار واعي
/// ومعاه تعديل في الواجهة — بس ماتعبّيهمش بالغلط وتفتكر إنك
/// بتصلّح.</para>
/// </summary>
public sealed record RepairWorkflowItem(
    string EventType,
    string EventTypeText,
    string FromStageText,
    string ToStageText,
    string ActorName,
    DateTime OccurredAtUtc,
    string Reason);
