using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using MediatR;

namespace Codlek.Application.Features.Maintenance.ClearOemCodeNames;

/// <summary>
/// بيمسح الاسم التجاري اللي أصله كود مصنّع — على الأجهزة <b>والفحوص</b>.
///
/// <para>🔴 <b>نفس القديم بالحرف</b> (<c>SearchTextBackfill.cs:164-202</c>):
/// <c>SystemFamily</c> على HP بيرجّع <c>103C_5336AN HP EliteBook</c> —
/// كود مش اسم. الاستقبال الجديد مابيكتبهوش على الجهاز، بس بيخزّنه على
/// <b>الفحص</b> زي ما الراكة بعتته، فصفحة الفحص بتفضل تعرضه.</para>
///
/// <para>🔴 <b>بنمسح، مابنصلّحش.</b> السيرفر مابيخزّنش <c>SystemFamily</c>
/// الخام، فمفيش مصدر يعيد الحساب منه. والمسح بيخلّي العرض يقع على
/// الموديل الخام — اللي هو الاسم الصح أصلاً.</para>
///
/// <para>⚠️ <b>الشرط ضيّق بقصد:</b> المصدر <c>SystemFamily</c> <b>و</b>
/// الاسم بادئ بكود وبعده مسافة (<see cref="Core.Devices.DeviceNaming.StartsWithOemCode"/>).
/// أي اسم تاني مابيتلمسش.</para>
/// </summary>
/// <param name="BatchSize">⚠️ للفحوص بس — الافتراضي هو اللي بيشتغل.</param>
public sealed record ClearOemCodeNamesCommand(Guid TenantId, int BatchSize = 500)
    : IRequest<Result<OemCleanupResult>>;
