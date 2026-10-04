namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// قرار المحاسب — والفني الجديد لو هيتغيّر.
///
/// <para>🔴 <b>الحقلين الاتنين اختياريين عن قصد.</b> فحوص المشروع
/// القديم بتبعت جسم فاضي (<c>{}</c>) على نقطة الموافقة وبتتوقّع
/// <c>404</c> — يعني السياسة عدّت والكود دوّر على الصف. ولو أي حقل
/// بقى إجباري، الرد بيبقى <c>400</c> والفحص بيقول إن الحاجز مكسور
/// وهو سليم.</para>
/// </summary>
public sealed record ApproveRepairRequest(Guid? TechnicianId, string? Note);
