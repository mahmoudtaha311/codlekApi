namespace Codlek.Application.Contracts.Rack;

/// <summary>
/// رد «الفني غيّر باسورده» — <b>أقل من رد الدخول</b>.
///
/// <para>🔴 <b>ومفيش <c>offlineValidUntilUtc</c> هنا عن قصد.</b>
/// تغيير الباسورد <b>مش دخول</b>: السيرفر بيستعمل صف المحاولة
/// الناجحة كدليل إن الفني كان مصرّح له وقت شغل أوفلاين، وتغيير
/// الباسورد مابيكتبش الصف ده. فتمديد المهلة على الراكة من غيره كان
/// بيخلّي الفني يشتغل أسبوع والسيرفر يرفض شغله كله وقت الرفع.
/// المهلة بتفضل من آخر دخول حقيقي.</para>
/// </summary>
/// <param name="MustChangePassword">
/// 🔴 <b>ودي الحاجة اللي الميزة كلها اتعملت عشانها.</b> العلامة
/// بتتحط عند الإنشاء وعند كل إعادة تعيين، ومكانش فيه ولا سطر في
/// النظام بيشيلها — فكانت متعلّمة على كل فني للأبد وشارة «باسورد
/// افتراضي» الحمرا شغّالة على الكل.
/// </param>
public sealed record TechnicianPasswordChanged(
    Guid TechnicianId,
    int CredentialVersion,
    bool MustChangePassword,
    string Message);
