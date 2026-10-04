using Codlek.Core.Enums;

namespace Codlek.Core.Auth;

/// <summary>
/// مين يقدر يتحكم في مين — <b>دوال نقية</b>.
///
/// <para>🔴 <b>القواعد دي أخطر حاجة في قطاع الحسابات.</b> أي خلل فيها
/// مش بيبان كعطل — بيبان كمدير مخزن بيترقّي نفسه لمالك، أو كمالك قافل
/// على نفسه الباب ومش عارف يفكّه.</para>
///
/// <para>⚠️ وعشان كده هي هنا منفصلة عن أي قاعدة بيانات: كل حالة ليها
/// فحص بيخلص في مللي ثانية، مش حالة أو اتنين على قاعدة حقيقية.</para>
/// </summary>
public static class UserManagementRules
{
    /// <summary>
    /// أقصر اسم مستخدم.
    ///
    /// <para>⚠️ الأرقام دي مربوطة بأعمدة القاعدة، مش أذواق.</para>
    /// </summary>
    public const int MinUsernameLength = 3;

    /// <summary>أطول اسم مستخدم — نفس طول عمود <c>Username</c>.</summary>
    public const int MaxUsernameLength = 60;

    public const int MinDisplayNameLength = 2;

    /// <summary>أطول اسم معروض — نفس طول عمود <c>DisplayName</c>.</summary>
    public const int MaxDisplayNameLength = 120;

    /// <summary>
    /// أطول سبب إيقاف — نفس طول عمود <c>SuspendedReason</c>.
    ///
    /// <para>⚠️ والسبب بينزل كمان جوّه <c>AuditEvent.Summary</c> اللي
    /// هو برضه <c>nvarchar(400)</c> ومعاه بادئة — يعني العمود التاني
    /// بيقع <b>قبل</b> ده. القص في <c>AuditTrail</c> بيحمي منها.</para>
    /// </summary>
    public const int MaxSuspendReasonLength = 400;

    /// <summary>أقصر سبب إيقاف — نص فاضي مش سبب.</summary>
    public const int MinSuspendReasonLength = 3;

    /// <summary>
    /// هل الحساب ده تحت سلطتي؟
    ///
    /// <para>🔴 <b>الشرط <c>targetId != myId</c> مش تفصيلة.</b> من
    /// غيره، المالك يقدر يوقف نفسه — يعني الشركة ممكن تفضل من غير ولا
    /// حساب مالك يقدر يفكّ الإيقاف. ده باب مقفول من جوّه مالوش
    /// مفتاح.</para>
    ///
    /// <para>⚠️ <b>ومدير الدور بياخد <c>false</c> دايماً.</b> هو داخل
    /// في سياسة <c>ManagerOrAbove</c> فبيوصل للنقط ويشوف القايمة — بس
    /// مابيعملش أي إجراء. ده سلوك الصفحة القديمة بالظبط، وتغييره
    /// بيدّيه صلاحية إدارة حسابات محدش طلبها.</para>
    ///
    /// <para>⚠️ <b>والمحاسب برّه خالص.</b> هو مش في
    /// <c>ManagerOrAbove</c> أصلاً، فمابيوصلش للنقط دي — والدالة
    /// بترجّع <c>false</c> برضه عشان الحاجزين ميختلفوش.</para>
    /// </summary>
    public static bool CanManage(
        UserRole myRole, Guid myId, Guid targetId, UserRole targetRole) =>
        targetId != myId &&
        myRole switch
        {
            UserRole.Owner => true,
            UserRole.Manager => targetRole == UserRole.Technician,
            _ => false,
        };

    /// <summary>
    /// أقدر أعمل حساب بالدور ده؟
    ///
    /// <para>🔴 مدير المخزن بيضيف <b>فنيين وبس</b>. من غير الشرط ده،
    /// أي مدير مخزن يقدر يعمل لنفسه حساب مالك تاني ويترقّى —
    /// وساعتها كل القواعد اللي فوق بتبقى شكلية.</para>
    /// </summary>
    public static bool CanCreate(UserRole myRole, UserRole wantedRole) =>
        myRole switch
        {
            UserRole.Owner => true,
            UserRole.Manager => wantedRole == UserRole.Technician,
            _ => false,
        };

    /// <summary>
    /// سبب الرفض بالنص — <b>اللي بيقراه بيعرف يعمل إيه بعده</b>.
    ///
    /// <para>⚠️ <c>403</c> من غير جسم بتسيب الواجهة مالهاش غير «مش
    /// مسموح» عامة. والفرق بين «ده حسابك إنت» و«ده أعلى من صلاحيتك»
    /// هو اللي بيخلّي المستخدم يعرف يروح لمين.</para>
    /// </summary>
    public static string DenialReason(Guid myId, Guid targetId) =>
        targetId == myId
            ? "مينفعش تتحكم في حسابك من هنا — استخدم صفحة الحساب"
            : "الحساب ده أعلى من صلاحيتك — كلّم المدير العام";
}
