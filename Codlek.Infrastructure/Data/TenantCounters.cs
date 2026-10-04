using Codlek.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// الأرقام المتسلسلة — <b>بـSQL خام، ومن غير أي لمس لمتتبّع التغييرات</b>.
/// </summary>
public sealed class TenantCounters(AppDbContext db) : ITenantCounters
{
    /// <summary>
    /// 🔴 <b>الزيادة والقراية في جملة واحدة.</b>
    ///
    /// <para><c>OUTPUT deleted.NextValue</c> بيرجّع القيمة <b>اللي
    /// كانت</b> قبل الزيادة — فالصف اللي بياخده المنادي محجوز له
    /// وحده. راكتين في نفس اللحظة بياخدوا رقمين مختلفين.</para>
    /// </summary>
    private const string Update = """
        UPDATE TenantCounters
           SET NextValue = NextValue + 1
        OUTPUT deleted.NextValue AS Value
         WHERE TenantId = {0} AND CounterName = {1}
        """;

    /// <summary>
    /// ⚠️ <b>إدخال مشروط — مش <c>Add</c> + <c>SaveChanges</c>.</b>
    /// </summary>
    private const string Insert = """
        INSERT INTO TenantCounters (TenantId, CounterName, NextValue)
        SELECT {0}, {1}, 2
         WHERE NOT EXISTS (SELECT 1 FROM TenantCounters
                            WHERE TenantId = {0} AND CounterName = {1})
        """;

    /// <summary>
    /// 🔴 <b>حجز بلوك — نفس الحركة بس بزيادة <c>count</c>.</b>
    ///
    /// <para><c>OUTPUT deleted.NextValue</c> بيرجّع أول رقم في
    /// المدى، والمدى كله بقى محجوز للمنادي في جملة واحدة.</para>
    /// </summary>
    private const string Reserve = """
        UPDATE TenantCounters
           SET NextValue = NextValue + {2}
        OUTPUT deleted.NextValue AS Value
         WHERE TenantId = {0} AND CounterName = {1}
        """;

    /// <summary>
    /// ⚠️ إدخال مشروط لبلوك — القيمة الجديدة <c>1 + count</c> لأن
    /// الرقم الأول (واحد) بيروح للمنادي.
    /// </summary>
    private const string InsertBlock = """
        INSERT INTO TenantCounters (TenantId, CounterName, NextValue)
        SELECT {0}, {1}, 1 + {2}
         WHERE NOT EXISTS (SELECT 1 FROM TenantCounters
                            WHERE TenantId = {0} AND CounterName = {1})
        """;

    /// <summary>
    /// 🔴 <b>المدى بيتحجز في جملة واحدة — مش حلقة.</b>
    ///
    /// <para>راكتين بيطلبوا في نفس اللحظة بياخدوا مديين
    /// <b>مختلفين ومتصلين</b>. والراكة محتاجة المدى متصل عشان
    /// توزّعه أوفلاين.</para>
    ///
    /// <para>⚠️ ونفس حكاية الإدخال المشروط اللي في
    /// <see cref="NextAsync"/> — من غير أي لمس للمتتبّع، عشان
    /// الدالة تبقى آمنة من جوّه حلقة دفعة.</para>
    /// </summary>
    public async Task<int> ReserveAsync(
        Guid tenantId, string counterName, int count, CancellationToken ct = default)
    {
        // ⚠️ صفر أو سالب بياخد واحد — الدالة مابترفضش، المنادي هو
        //    اللي بيقص القيمة قبلها.
        int size = Math.Max(1, count);

        var taken = await db.Database
            .SqlQueryRaw<int>(Reserve, tenantId, counterName, size)
            .ToListAsync(ct);

        if (taken.Count > 0) return taken[0];

        try
        {
            int created = await db.Database.ExecuteSqlRawAsync(
                InsertBlock, [tenantId, counterName, size], ct);

            if (created > 0) return 1;
        }
        catch (Exception)
        {
            // حد تاني سبقنا — الزيادة تحت بتحسمها.
        }

        var retry = await db.Database
            .SqlQueryRaw<int>(Reserve, tenantId, counterName, size)
            .ToListAsync(ct);

        return retry.Count > 0 ? retry[0] : 1;
    }

    public async Task<int> NextAsync(
        Guid tenantId, string counterName, CancellationToken ct = default)
    {
        var taken = await db.Database
            .SqlQueryRaw<int>(Update, tenantId, counterName)
            .ToListAsync(ct);

        if (taken.Count > 0) return taken[0];

        /*
          مفيش عدّاد لسه — بنعمله وناخد الرقم الأول.

          🔴 **بـSQL خام، ومن غير أي لمس للمتتبّع — ودي مش تفصيلة.**

          النسخة الأولى في المشروع القديم كانت `Add` + `SaveChangesAsync`
          على السياق المشترك، وجوّه `catch` عام بتنادي
          `ChangeTracker.Clear()`. وده كان مقبول وقت ما الدالة كانت
          بتتنادى من تسجيل الراكة بس — السياق ساعتها فاضي.

          ⚠️ **بس هي بتتنادى كمان من جوّه حلقة دفعة المزامنة**، والسياق
          ساعتها ماسك أمر صيانة لسه نص مبني. والنتيجة كانت:

          ١ · الحفظ بيدفع صف ناقص قبل أوانه
          ٢ · وأي `DbUpdateException` من أي كيان تاني في الدفعة
              (مفتاح أجنبي غلط مثلاً) يتبلع على إنه «سباق عدّاد»
          ٣ · وبعدين `Clear()` بتفصل **الدفعة كلها**

          فالسيرفر يرد «اتقبل» ومفيش ولا صف اتكتب — **فقد بيانات صامت
          متقنّع بنجاح**.

          والإدخال المشروط تحت مابيعديش على المتتبّع خالص، فالدالة آمنة
          من أي مكان.
        */
        try
        {
            int created = await db.Database.ExecuteSqlRawAsync(
                Insert, [tenantId, counterName], ct);

            if (created > 0) return 1;
        }
        catch (Exception)
        {
            // حد تاني سبقنا بين الفحص والإدخال — الزيادة تحت بتحسمها.
            // ⚠️ ومفيش `Clear()` هنا: مفيش حاجة اتتبعت أصلاً.
        }

        // العدّاد بقى موجود (إحنا أو غيرنا عمله) — ناخد رقمنا.
        var retry = await db.Database
            .SqlQueryRaw<int>(Update, tenantId, counterName)
            .ToListAsync(ct);

        return retry.Count > 0 ? retry[0] : 1;
    }
}
