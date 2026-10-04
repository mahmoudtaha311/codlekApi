using Codlek.Core.Enums;

namespace Codlek.Core.Racks;

/// <summary>
/// قواعد إدارة محطات الفحص.
///
/// <para>⚠️ <b>دي قواعد إدارة بس — مش تسجيل المحطة ولا التحقق من
/// مفتاحها.</b> دول عايشين في مرحلة الراكة (<c>/api/v1/rack/*</c>)
/// وبيتحققوا بمفتاح محطة مش بتوكن مستخدم.</para>
/// </summary>
public static class RackPolicy
{
    /// <summary>
    /// أقصر اسم محطة مقبول.
    ///
    /// <para>⚠️ حرف واحد مش اسم — والاسم ده هو اللي بيظهر جنب كل فحص
    /// رفعته المحطة، فـ«م» بتخلّي سجل كامل مش منسوب لحد.</para>
    /// </summary>
    public const int MinStationName = 2;

    /// <summary>
    /// 🔴 <b>أقصر سبب إلغاء مقبول.</b>
    ///
    /// <para>الإلغاء نهائي ومش بيرجع — فالسبب هو الحاجة الوحيدة اللي
    /// بتفضل تشرح ليه مفتاح محطة اتلغى. و«لأ» مش سبب.</para>
    /// </summary>
    public const int MinRevokeReason = 3;

    /// <summary>
    /// تغيير حالة المحطة — <b>الملغية نهائياً مابترجعش</b>.
    ///
    /// <para>🔴 الرجوع معناه إن مفتاح <b>اتلغى</b> يبقى صالح تاني —
    /// واللي اتلغى غالباً اتلغى عشان اتسرق أو عشان الراكة اتستنسخت.
    /// الرجوع من الإيقاف دوسة زرار، والرجوع من الإلغاء كود تفعيل
    /// جديد.</para>
    /// </summary>
    public static bool CanChangeStatus(RackStatus current) =>
        current != RackStatus.Revoked;

    /// <summary>
    /// الكود ده ينفع يتمسح؟
    ///
    /// <para>⚠️ <b>والفحص ده مش تكرار للفلتر بتاع القايمة.</b>
    /// القايمة مابتعرضش المستهلك، بس المسح بياخد <b>معرّف</b> —
    /// ومعرّف كود مستهلك ممكن يتبعت من سكريبت أو من صفحة قديمة
    /// مفتوحة في تاب. الحارس لازم يبقى على السيرفر.</para>
    ///
    /// <para>⚠️ والمقارنة <c>&gt;</c> مش <c>&gt;=</c>: الكود اللي
    /// وقت انتهاءه <b>هو</b> اللحظة دي بالظبط يبقى منتهي — نفس
    /// الحساب اللي <c>IsExpired</c> في القايمة بيتعرض بيه، عشان
    /// الصف اللي الواجهة بتوريه «منتهي» مايترفضش.</para>
    /// </summary>
    public static CodeDeletion Deletion(
        DateTime? consumedAtUtc, DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (consumedAtUtc != null) return CodeDeletion.AlreadyUsed;

        return expiresAtUtc > nowUtc ? CodeDeletion.StillValid : CodeDeletion.Allowed;
    }

    /// <summary>
    /// الكود منتهي؟ — <b>نفس الحساب اللي المسح بيعتمد عليه</b>.
    ///
    /// <para>🔴 دالة واحدة للاتنين عن قصد. لو القايمة حسبت الانتهاء
    /// بـ<c>&lt;=</c> والمسح بـ<c>&lt;</c>، كان فيه ثانية واحدة الصف
    /// فيها باين «منتهي» والمسح بيرفضه — والمدير بيدوس ويلاقي رسالة
    /// بتقول العكس اللي شايفه.</para>
    /// </summary>
    public static bool IsExpired(DateTime expiresAtUtc, DateTime nowUtc) =>
        expiresAtUtc <= nowUtc;
}
