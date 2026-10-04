namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// ماركات الفني — <b>استبدال كامل</b>.
///
/// <para>🔴 <b>والجسم الفاضي معناه «مفيش قيد» بقرار صريح</b>، مش
/// «ماتلمسش». مسار التعديل بيفرّق بين الاتنين بقيمة حارسة، ومجموعة
/// مالهاش قيمة حارسة تنفع — فالاستبدال الكامل هو العقد.</para>
/// </summary>
public sealed record SetTechnicianBrandsRequest(List<Guid>? BrandIds);
