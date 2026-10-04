using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Text;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// بيضيف صف في <c>AuditEvents</c> — <b>ومابيحفظش</b>.
/// </summary>
public sealed class AuditTrail(AppDbContext db, ICurrentUser me) : IAuditTrail
{
    /// <summary>أطول وصف — نفس طول <c>AuditEvent.Summary</c> في القاعدة.</summary>
    private const int MaxSummaryLength = 400;

    public void Record(
        string action, string entityType, Guid? entityId,
        string entityCode, string summary) =>
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = me.TenantId,
            ActorType = "User",
            ActorUserId = me.Id,
            ActorName = me.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityCode = entityCode,
            Summary = Clamp(summary),
        });

    /// <summary>
    /// 🔴 <b>سطر فاعله محطة — والشركة من المحطة مش من التوكن.</b>
    ///
    /// <para>طلب الراكة مالوش توكن، و<c>ICurrentUser.TenantId</c>
    /// <b>بترمي</b> لو اتندهت عليه. فالدالة دي <b>عمرها ما بتلمس</b>
    /// <c>me</c> — وده مقصود: نداء واحد غلط على مسار المزامنة
    /// معناه <c>500</c>، والراكة بتعيد المحاولة للأبد.</para>
    ///
    /// <para>⚠️ <b>و<c>ActorType = "Rack"</c> بحرف كبير</b> — نفس
    /// اللي القديم بيكتبه، وترجمة الفاعل في سجل المراجعة بتقارن
    /// بحروف صغيرة فالاتنين بيعدّوا. بس بيانات الإنتاج فيها
    /// <c>"Rack"</c>، والنقل زي ما هو.</para>
    /// </summary>
    public void RecordForRack(
        RackAuditActor actor,
        string action, string entityType, Guid? entityId,
        string entityCode, string summary, string dataJson = "") =>
        db.AuditEvents.Add(new AuditEvent
        {
            // 🔴 من المحطة — مش من `me`.
            TenantId = actor.TenantId,

            ActorType = "Rack",
            ActorRackId = actor.RackId,
            ActorName = actor.Name,

            // ⚠️ العنوان هو اللي بيفرّق بين المحطة اللي في الورشة
            //    وحد شايل نسخة من قرصها.
            Ip = TextClip.To(actor.Ip, 60),

            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityCode = entityCode,
            Summary = Clamp(summary),

            // ⚠️ فاضي مش null — العمود مابيقبلهاش.
            DataJson = dataJson ?? "",
        });

    /// <summary>
    /// ⚠️ <b>القص هنا شبكة أمان، مش بديل عن التحقق عند النقطة.</b>
    ///
    /// <para>اللي بيكتب سبب طويل المفروض يشوف رسالة تقوله يختصره، مش
    /// يلاقي سببه اتقص في صمت. بس لو عدّى، الصف لازم ينزل — سجل
    /// مقصوص أحسن من عملية بترمي على طول نص.</para>
    /// </summary>
    private static string Clamp(string? summary)
    {
        summary ??= "";

        return summary.Length <= MaxSummaryLength
            ? summary
            : summary[..(MaxSummaryLength - 1)] + "…";
    }
}
