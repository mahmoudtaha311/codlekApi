using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using MediatR;

namespace Codlek.Application.Features.Maintenance.ResolveOrphanReports;

/// <summary>
/// بيربط الفحوص اللي <b>مالهاش جهاز</b> بأجهزتها لو مراسيها بتطابق جهاز
/// موجود — وإلا بيعلّمها للمراجعة.
///
/// <para>🔴 <b>من غيره الفحص اللي يوصل قبل جهازه يفضل يتيم للأبد.</b>
/// الربط في الجديد بيحصل وقت الاستقبال بس، ومزامنة الأجهزة مابتلمسش
/// الفحوص. فالفحص بيختفي من تاريخ اللاب، وتنبيه «فحوص من غير جهاز»
/// مابينزلش أبداً. القديم كان بيعمل اللفة دي مع كل إقلاع
/// (<c>DeviceResolutionBackfill.cs:35-102</c>).</para>
///
/// <para>🔴 <b>ومفيش إنشاء أجهزة هنا خالص.</b> الفحوص القديمة مراسيها
/// ضعيفة — «Default string» بتتكرر على مئات اللابات. جهاز لكل فحص قديم
/// معناه آلاف الصفوف الوهمية.</para>
///
/// <para>⚠️ <b>والمطابقة هي نفس دالة الاستقبال</b>
/// (<see cref="Rack.IngestReports.DeviceAnchorMatch"/>) — نفس الترتيب ونفس
/// الاستعلام. أي اختلاف = نفس اللاب بهويتين.</para>
/// </summary>
/// <param name="BatchSize">
/// ⚠️ ٢٠٠ مش ٥٠٠ زي القديم: كل صف معاه نسخته الخام (~٢٠ كيلوبايت على
/// الإنتاج)، والاستضافة رامها قليل.
/// </param>
public sealed record ResolveOrphanReportsCommand(Guid TenantId, int BatchSize = 200)
    : IRequest<Result<OrphanSweepResult>>;
