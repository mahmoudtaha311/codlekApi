namespace Codlek.Application.Contracts.Users;

/// <summary>
/// صف واحد في قايمة حسابات اللوحة.
///
/// <para>🔴 <b>ترتيب الحقول هنا مش تزويق — هو اللي بيمنع عطل
/// صامت.</b> فيه خمس نصوص ورا بعض (<c>Username · DisplayName · Code ·
/// Role · RoleText</c>) وبعدين نصّين تانيين ورا بعض
/// (<c>SuspendedReason · SuspendedByName</c>). قلب اتنين متجاورين من
/// نفس النوع <b>بيتترجم من غير ولا تحذير</b>، والنتيجة صفحة بتعرض كود
/// ٦ أرقام مكان اسم كل واحد، أو اسم اللي أوقف الحساب مكان سبب
/// الإيقاف. <b>ده حصل فعلاً في المشروع القديم.</b></para>
/// </summary>
public sealed record UserAccountItem(
    Guid Id,
    string Username,
    string DisplayName,

    /// <summary>كود الحساب — ده اللي بيربطه بالفحوصات.</summary>
    string Code,

    /// <summary>اسم قيمة <c>UserRole</c> — الواجهة بتقارن بيه.</summary>
    string Role,

    /// <summary>
    /// الدور بالعربي المعروض.
    ///
    /// <para>⚠️ الترجمة بتخرج من السيرفر جنب القيمة الخام عن قصد. لو
    /// الواجهة ترجمت لوحدها، أول دور جديد بيظهر بالإنجليزي في شاشة
    /// وبالعربي في التانية.</para>
    /// </summary>
    string RoleText,

    bool IsActive,

    /// <summary>
    /// سبب الإيقاف — <b>المستخدم بيشوفه في شاشة الدخول</b>.
    ///
    /// <para>🔴 مش توثيق. من غيره الموقوف بيقف قدام رسالة مقفولة مش
    /// عارف يكلّم مين، فبيروح يجرّب باسورد زميله — وده بالظبط اللي
    /// الإيقاف بيمنعه.</para>
    /// </summary>
    string SuspendedReason,

    string SuspendedByName,
    DateTime? SuspendedAtUtc,

    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime? LastLoginUtc,

    /// <summary>
    /// هل <b>اللي بيطلب</b> يقدر يتحكم في الصف ده؟
    ///
    /// <para>🔴 بيتحسب على السيرفر لكل صف على حدة، مش في الواجهة.
    /// الواجهة بتقفل الأزرار بيه <b>عشان تسهّل</b>، والسيرفر بيتأكد
    /// تاني في كل نقطة. القاعدة مكتوبة مرة واحدة، فمفيش زرار ظاهر
    /// بيرجّع <c>403</c> ولا زرار مخفي عن حد من حقه يضغطه.</para>
    ///
    /// <para>⚠️ وبيبقى <c>false</c> على صف المستخدم نفسه — حتى
    /// للمالك.</para>
    /// </summary>
    bool CanManage);
