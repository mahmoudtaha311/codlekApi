namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// إيقاف فني — <b>والسبب إجباري</b>.
///
/// <para>🔴 الفني بيشوف السبب على شاشة الراكة. ومن غيره بيقف قدام
/// رسالة مقفولة ومش عارف يكلّم مين، فبيروح يجرّب حساب زميله — وده
/// بالظبط اللي الإيقاف بيمنعه.</para>
/// </summary>
public sealed record SuspendTechnicianRequest(string? Reason);
