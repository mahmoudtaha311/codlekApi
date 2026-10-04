using Codlek.Application.Interfaces;
using Codlek.Core.Entities;

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
