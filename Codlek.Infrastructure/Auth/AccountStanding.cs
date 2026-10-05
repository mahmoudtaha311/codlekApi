using Codlek.Application.Interfaces;
using Codlek.Core.Auth;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// الفحص اللي بيطرد الحساب الموقوف <b>من أول طلب</b> — زي القديم.
///
/// <para>🔴 <b>كل طلب لوحة بتوكن بيعدّي هنا</b> (من <c>OnTokenValidated</c>
/// في <c>Codlek.Api/DependencyInjection.cs</c>). الحساب الموقوف، أو اللي
/// باسورده اتغيّر أو اتعاد تعيينه، أو اللي اتمسح — توكنه بيترفض بـ
/// <c>401</c> عادي، نفس رد التوكن المنتهي، فاللوحة بتحاول تجدّد
/// وبيترفض التجديد وبتروح على صفحة الدخول.</para>
///
/// <para>⚠️ <b>والتكلفة: استعلام صغير بالمعرّف، متخزّن <see cref="Window"/>.</b>
/// تلات أعمدة من صف واحد بالمفتاح الأساسي. واللوحة بتضرب كذا طلب ورا
/// بعض في كل شاشة، فالكاش بيخلّيها استعلام واحد كل كام ثانية للمستخدم.</para>
///
/// <para>🔴 <b>والكاش مابيأخّرش الطرد على السيرفر ده:</b> الإيقاف وإعادة
/// التعيين وتغيير الباسورد والتفعيل بينادوا <see cref="Forget"/> بعد
/// الحفظ. التأخير (لحد <see cref="Window"/>) بيحصل بس لو التغيير جه من
/// برّه — نسخة سيرفر تانية أو حد كتب في القاعدة بإيده.</para>
/// </summary>
public sealed class AccountStanding(IMemoryCache cache) : IAccountStanding
{
    /// <summary>
    /// عمر الحالة المتخزّنة.
    ///
    /// <para>⚠️ ده أقصى تأخير للطرد لو التغيير ماعدّاش على السيرفر ده.
    /// أطول = استعلامات أقل وطرد أبطأ؛ والمطلوب ≤ ١٠ ثواني.</para>
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 🔴 <b>عدّاد بيزيد مع كل <see cref="Forget"/> — وده اللي بيقفل السباق.</b>
    ///
    /// <para>من غيره: طلب بيقرا الحالة <b>قبل</b> حفظ الإيقاف بلحظة،
    /// الإيقاف بيتحفظ وبيمسح الكاش، والطلب يرجع يخزّن الحالة القديمة
    /// (مفعّل) <b>بعد</b> المسح — فالموقوف يفضل شغّال عمر الكاش كله.
    /// بالعدّاد: لو حصل أي نسيان وإحنا بنقرا، مابنخزّنش اللي قريناه.</para>
    /// </summary>
    private long _generation;

    private sealed record Snapshot(bool Exists, bool IsActive, Guid TenantId, int Version)
    {
        public static readonly Snapshot Missing = new(false, false, Guid.Empty, -1);
    }

    /// <summary>صاحب التوكن لسه مسموحله؟</summary>
    public async Task<bool> AllowsAsync(
        AppDbContext db, Guid userId, Guid tokenTenant, int tokenVersion,
        CancellationToken ct = default)
    {
        string key = Key(userId);

        if (!cache.TryGetValue(key, out Snapshot? snapshot) || snapshot is null)
        {
            long before = Interlocked.Read(ref _generation);

            // ⚠️ إسقاط لتلات أعمدة ومن غير تتبّع — مش الصف كله.
            var row = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.TenantId, u.CredentialVersion })
                .FirstOrDefaultAsync(ct);

            snapshot = row is null
                ? Snapshot.Missing
                : new Snapshot(true, row.IsActive, row.TenantId, row.CredentialVersion);

            if (Interlocked.Read(ref _generation) == before)
                cache.Set(key, snapshot, Window);
        }

        return AccessTokenRules.Allows(
            accountExists: snapshot.Exists,
            accountActive: snapshot.IsActive,
            accountTenant: snapshot.TenantId,
            accountVersion: snapshot.Version,
            tokenTenant: tokenTenant,
            tokenVersion: tokenVersion);
    }

    public void Forget(Guid userId)
    {
        // ⚠️ العدّاد الأول، وبعدين المسح — عكسهم بيفتح نفس السباق.
        Interlocked.Increment(ref _generation);
        cache.Remove(Key(userId));
    }

    private static string Key(Guid userId) => "account-standing:" + userId.ToString("N");
}
