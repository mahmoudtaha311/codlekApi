using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces;

/// <summary>
/// المستخدم الحالي — <b>مقروء من التوكن، مش من الطلب</b>.
///
/// <para>🔴 <b>الشركة بتيجي من هنا دايماً.</b> لو جت من الطلب، مدير
/// شركة كان هيقدر يقرا ويعدّل بيانات شركة تانية بإنه يبعت
/// <c>tenantId</c> مختلف. والطلب ده مش هيبان غلط في أي سجل.</para>
///
/// <para>⚠️ وده السبب إن الواجهة دي موجودة بدل ما كل Handler يقرا
/// <c>HttpContext</c>: طبقة التطبيق مالهاش علاقة بـHTTP، ولو كل
/// واحد قرا لوحده كان أول واحد ينسى الترشيح بالشركة يفتح البيانات
/// كلها.</para>
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    Guid TenantId { get; }
    string DisplayName { get; }
    string Code { get; }
    UserRole Role { get; }

    /// <summary>فيه مستخدم أصلاً؟ — <c>false</c> يعني طلب مجهول.</summary>
    bool IsAuthenticated { get; }

    // =================================================================
    //  أعلام الدور — مشتقّة، مش مخزّنة
    // =================================================================

    /// <summary>
    /// مدير أو فوق — <b>ومدير الدور جوّاهم</b>.
    ///
    /// <para>⚠️ <b>لازم تفضل مطابقة لسياسة <c>ManagerOrAbove</c>
    /// بالحرف.</b> اختلافهم بيدّي حاجز مفتوح على المسار وقفل جوّه
    /// النقطة — ونفس المستخدم بياخد رسالتين مختلفتين على حسب اللي
    /// رفضه.</para>
    /// </summary>
    bool IsManagerOrAbove =>
        Role is UserRole.Manager or UserRole.Owner or UserRole.FloorManager;

    /// <summary>المالك — للتجاوزات الإدارية.</summary>
    bool IsOwner => Role == UserRole.Owner;

    /// <summary>
    /// بيقدر يوافق على الصيانة — <b>المحاسب والمالك</b>.
    ///
    /// <para>🔴 <b>وده هو اللي بيقرّر منع الماركة في الإسناد في
    /// صمت.</b> اللي بيوافق بيقدر يسند فني برّه ماركاته، واللي
    /// مابيوافقش لأ. فأي اختلاف بين الخاصية دي وسياسة
    /// <c>RepairApprover</c> معناه إن المحاسب بيعدّي الحاجز ومايقدرش
    /// يغيّر الفني — أو العكس، إن المدير بيعدّي القيد وهو مش من
    /// حقه.</para>
    ///
    /// <para>⚠️ <b>وممنوع تتدمج في <c>IsManagerOrAbove</c>.</b>
    /// المحاسب مش مدير والمدير مش موافق — والدمج بيخلّي الاتنين نفس
    /// الحاجة.</para>
    /// </summary>
    bool IsRepairApprover => Role is UserRole.Accountant or UserRole.Owner;
}
