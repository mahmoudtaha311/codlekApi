namespace Codlek.Core.Repairs;

/// <summary>
/// أرقام الوقت على أمر الصيانة — <b>تلات أسئلة مختلفة، مش رقم واحد</b>.
///
/// <para>🔴 <b>وnowUtc بتتبعت مش بتتقرا جوّه.</b> كده الصفحة كلها
/// بتتقاس على نفس اللحظة، والدوال تتفحص من غير قاعدة.</para>
/// </summary>
public static class RepairTiming
{
    /// <summary>
    /// المدة من البدء للإنهاء بالمللي ثانية — <c>null</c> لو لسه ماخلصتش.
    ///
    /// <para>⚠️ <b>دي مش وقت شغل فعلي.</b> مفيش إيقاف مؤقت في المرحلة
    /// دي، فأمر اتفتح الصبح واتقفل بالليل بيقول ٨ ساعات حتى لو الفني
    /// اشتغل فيه نص ساعة. اقراها «قد إيه قعد مفتوح».</para>
    ///
    /// <para>⚠️ والسالب بيتقصّ على صفر — ساعة راكة قدّامها.</para>
    /// </summary>
    public static long? DurationMs(DateTime? startedAtUtc, DateTime? completedAtUtc)
    {
        if (startedAtUtc is not { } start || completedAtUtc is not { } end) return null;

        long ms = (long)(end - start).TotalMilliseconds;
        return ms < 0 ? 0 : ms;
    }

    /// <summary>
    /// «الأمر ده مفتوح بقاله كام ساعة» — من <b>الفتح</b> لدلوقتي.
    ///
    /// <para>🔴 <b>المفتوح بس.</b> الأمر اللي خلص بيرجّع <c>null</c>,
    /// مش الوقت اللي استغرقه — ده <see cref="DurationMs"/> شغله.
    /// خلطهم بيخلّي أمر قفل من شهر يطلع «عمره ٧٢٠ ساعة» ويغرق اللي
    /// لسه واقف.</para>
    /// </summary>
    public static double? OpenAgeHours(
        DateTime openedAtUtc, DateTime? completedAtUtc, DateTime nowUtc)
    {
        if (completedAtUtc != null) return null;

        return Math.Max(0, (nowUtc - openedAtUtc).TotalHours);
    }

    /// <summary>
    /// «الأمر ده تحت إيد فني بقاله كام ساعة» — من <b>البدء</b> لدلوقتي.
    ///
    /// <para>🔴 <b>مش نفس <see cref="OpenAgeHours"/>.</b> أمر اتفتح من
    /// أسبوعين وبدأ من ساعة عنده <c>OpenAgeHours = 336</c> و
    /// <c>RepairAgeHours = 1</c> — ورقم واحد منهم لوحده بيدّي صورة
    /// غلط في الاتجاهين.</para>
    ///
    /// <para>⚠️ <b>مفيش بداية = <c>null</c>، مش صفر.</b> الصفر كان
    /// هيخلّي الأمر اللي محدش استلمه يطلع في أول «الأقدم الأول» كأنه
    /// أطول واحد.</para>
    /// </summary>
    public static double? RepairAgeHours(
        DateTime? startedAtUtc, DateTime? completedAtUtc, DateTime nowUtc)
    {
        if (completedAtUtc != null) return null;
        if (startedAtUtc is not { } started) return null;

        return Math.Max(0, (nowUtc - started).TotalHours);
    }
}
