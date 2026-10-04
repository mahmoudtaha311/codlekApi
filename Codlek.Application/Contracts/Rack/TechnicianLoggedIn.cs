namespace Codlek.Application.Contracts.Rack;

/// <summary>
/// رد دخول الفني — <b>وهو الأقل الممكن</b>.
///
/// <para>🔴 <b>مفيش بصمة ولا ملح ولا باسورد ولا مفتاح المحطة.</b>
/// المحطة عندها المفتاح أصلاً، وإرجاعه بيحطّه في لوج كل بروكسي في
/// الطريق من غير أي مكسب.</para>
///
/// <para>⚠️ <b>وأسماء الحقول دي الراكة بتقراها حرف بحرف</b>
/// (<c>JsonDocument.TryGetProperty</c>)، فأي تغيير في اسم أو حالة
/// حروف معناه إن الراكة بتلاقي الحقل ناقص وبتحط القيمة الافتراضية —
/// ساكتة.</para>
/// </summary>
/// <param name="CredentialVersion">
/// 🔴 <b>النسخة دي هي اللي بتقفل الأجهزة التانية.</b> الراكة
/// بتخزّنها، والمدير لما يغيّر باسورد الفني النسخة بتزيد — فأي محطة
/// لسه فاكرة النسخة القديمة بتعرف إنها بايتة.
/// </param>
/// <param name="OfflineValidUntilUtc">
/// 🔴 <b>آخر وقت تنفع الراكة تدخّل الفني فيه وهي أوفلاين.</b>
///
/// <para>والراكة بتعتمد عليه فعلاً: لو جه <c>null</c> بتقفل الدخول
/// الأوفلاين على الفني خالص — حتى لو عندها نسخة محفوظة
/// صالحة.</para>
/// </param>
/// <param name="CanTest">
/// 🔴 <b>قدرات الفني بتنزل مع الدخول.</b>
///
/// <para>الراكة بتشتغل أوفلاين، فلازم تعرف تقفل شاشة الصيانة من غير
/// ما تسأل السيرفر كل مرة. من غير الحقول دي، الشاشة كانت هتتفتح لأي
/// حد والسيرفر يرفض الشغل <b>بعد</b> ما يتعمل.</para>
///
/// <para>⚠️ <b>والراكة بتفترض <c>true</c> لو الحقل ناقص</b> — فشيله
/// مش «إضافة بتتجاهل»، هو فتح للشاشة.</para>
/// </param>
public sealed record TechnicianLoggedIn(
    Guid TechnicianId,
    string TechnicianCode,
    string DisplayName,
    int CredentialVersion,
    bool MustChangePassword,
    DateTime? OfflineValidUntilUtc,
    bool CanTest,
    bool CanRepair,
    string RackCode,
    DateTime ServerTimeUtc);
