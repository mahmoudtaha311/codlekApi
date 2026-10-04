namespace Codlek.Core.Sync;

/// <summary>
/// «الفني ده كان مصرّح له وقت ما عمل الحركة؟» — <b>القرار النقي</b>.
///
/// <para>🔴 <b>القاعدة: سحب الصلاحية بيمنع الشغل الجاي، مابيلغيش اللي
/// خلص.</b> الراكة بتشتغل أوفلاين، فممكن فني يعمل صيانة وهو مصرّح له
/// وبعدين الصلاحية تتسحب قبل ما الشغل يترفع. من غير التفرقة بين «كان
/// مصرّح له وقتها» و«مكانش مصرّح له خالص»، سحب صلاحية <b>بيمسح شغل
/// حصل فعلاً</b> — جهاز اتصلّح ومفيش سجل بيقول مين صلّحه.</para>
///
/// <para>⚠️ <b>والترتيب نفسه هو الأمان:</b> الوقت ← الدخول المسجّل ←
/// النافذة ← الإيقاف ← الصلاحية. ومكتوب كدالة نقية عشان كل فرع
/// يتجرّب من غير قاعدة.</para>
/// </summary>
public static class OfflineAuthorisation
{
    /// <summary>
    /// سماحية فرق الساعة بين الراكة والسيرفر.
    ///
    /// <para>⚠️ ساعة الراكة مش موثوقة — جهاز في ورشة ممكن يتظبط غلط.
    /// السماحية دي بتستوعب الفرق الطبيعي من غير ما تفتح باب لتاريخ
    /// مستقبلي مخترع.</para>
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromHours(6);

    public const string FutureMessage =
        "وقت الحركة في المستقبل — ساعة المحطة محتاجة ضبط.";

    public const string NoLoginMessage =
        "مفيش تسجيل دخول مسجّل للفني ده على المحطة دي قبل الحركة.";

    public const string ExpiredMessage =
        "تصريح الفني الأوفلاين كان خلص وقت الحركة دي.";

    public const string SuspendedMessage = "الفني كان موقوف وقت الحركة دي.";

    public const string NoCapabilityMessage = "الفني ده مالوش الصلاحية دي وقت الحركة.";

    /// <summary>القرار — <c>null</c> = مصرّح، وإلا السبب.</summary>
    /// <param name="occurredAtUtc">وقت الحركة زي ما الراكة كتبته.</param>
    /// <param name="nowUtc">ساعة السيرفر.</param>
    /// <param name="lastLoginAtUtc">
    /// 🔴 <b>آخر دخول ناجح مسجّل على السيرفر</b> للفني ده <b>على
    /// المحطة دي</b>، في وقت الحركة أو قبله — أو <c>null</c>.
    /// </param>
    /// <param name="offlineValidityDays">طول النافذة من الإعدادات.</param>
    /// <param name="isActive">الفني شغّال دلوقتي؟</param>
    /// <param name="suspendedAtUtc">وقت الإيقاف لو موقوف.</param>
    /// <param name="hasCapability">الصلاحية شغّالة دلوقتي؟</param>
    /// <param name="capabilityChangedAtUtc">آخر مرة الصلاحية اتغيّرت.</param>
    public static string? Deny(
        DateTime occurredAtUtc,
        DateTime nowUtc,
        DateTime? lastLoginAtUtc,
        int offlineValidityDays,
        bool isActive,
        DateTime? suspendedAtUtc,
        bool hasCapability,
        DateTime? capabilityChangedAtUtc)
    {
        // ١) وقت من المستقبل مش دليل على حاجة.
        if (occurredAtUtc > nowUtc.Add(ClockSkew)) return FutureMessage;

        /*
          ٢) 🔴 **نافذة تصريح السيرفر نفسه كتبها.**

          ده الفرق بين قاعدة حقيقية وقاعدة على النية: من غير الخطوة
          دي، الراكة كانت تقدر تكتب أي تاريخ قديم في الحمولة وتعدّي.
          لازم يبقى فيه **دخول ناجح مسجّل على السيرفر** للفني ده على
          المحطة دي، والحركة جوّه نافذة الصلاحية بتاعته.
        */
        if (lastLoginAtUtc is not { } authorisedAt) return NoLoginMessage;

        DateTime expires = authorisedAt.AddDays(Math.Max(0, offlineValidityDays));

        if (occurredAtUtc > expires) return ExpiredMessage;

        /*
          ٣) **الإيقاف بيقطع النافذة من ساعته.**

          نافذة قديمة مابتديش حق شغل بعد الإيقاف: الدخول بيفشل بعد
          الإيقاف، بس النافذة اللي اتفتحت قبله ممكن تكون لسه سارية.
          ⚠️ والشغل **قبل** الإيقاف بيعدّي — الإيقاف بيمنعه يدخل من
          بكرة، والصيانة اللي عملها إمبارح حصلت.
        */
        if (!isActive && suspendedAtUtc is { } suspendedAt && occurredAtUtc >= suspendedAt)
            return SuspendedMessage;

        // ٤) وأخيراً الصلاحية نفسها.
        if (hasCapability) return null;

        /*
          ⚠️ **الصلاحية مسحوبة — بس الحركة حصلت قبل السحب؟ تعدّي.**

          ومفيش وقت سحب مسجّل = مفيش دليل إنها كانت شغّالة يوم من
          الأيام = رفض.
        */
        if (capabilityChangedAtUtc is { } changedAt && occurredAtUtc < changedAt)
            return null;

        return NoCapabilityMessage;
    }
}
