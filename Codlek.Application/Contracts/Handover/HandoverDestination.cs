namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// جهة ينفع اللاب يتسلّم لها.
///
/// <para>⚠️ <c>Kind</c> اسم القيمة كنص — الواجهة بتعرض أيقونة
/// مختلفة للمبيعات.
/// </para>
/// </summary>
public sealed record HandoverDestination(Guid Id, string Code, string Name, string Kind);
