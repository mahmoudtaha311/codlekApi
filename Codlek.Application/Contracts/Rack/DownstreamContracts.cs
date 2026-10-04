namespace Codlek.Application.Contracts.Rack;

/// <summary>
/// فني صيانة زي ما الراكة محتاجاه عشان تسنده أمر.
///
/// <para>🔴 <b>ومفيش أي بيانات اعتماد هنا — لا بصمة ولا ملح ولا
/// اسم دخول.</b> ده عقد «مين ينفع يتسند» مش عقد دخول: الراكة
/// مابتسجّلش دخول حد من القايمة دي، هي بتحط اسمه على أمر.</para>
/// </summary>
/// <param name="CanRepair">
/// ⚠️ بيترجع دايماً <c>true</c> في التغذية دي، وموجود عن قصد —
/// الراكة بتخزّن القيمة زي ما جت، فلو يوم ما التغذية رجّعت فنيين
/// بعلامة، الراكة القديمة هتحترمها من غير تحديث.
/// </param>
public sealed record RepairTechnicianRow(
    Guid Id,
    string Code,
    string DisplayName,
    bool CanRepair,
    bool IsActive);

public sealed record RepairRosterResponse(
    IReadOnlyList<RepairTechnicianRow> Technicians,
    DateTime ServerTimeUtc);

/// <summary>حاوية استيراد زي ما الراكة محتاجاها في الليستة.</summary>
/// <param name="Code">الرمز زي ما اتكتب — ده اللي الراكة بتبعته لما تزامن.</param>
/// <param name="DeviceCount">كام لاب متسجّل في الحاوية دي — للعرض.</param>
public sealed record ContainerRow(
    Guid Id,
    string Code,
    string Name,
    int DeviceCount);

/// <summary>
/// ⚠️ <b>والليستة دي مش سلطة.</b> الحاوية إجبارية، بس <b>الكتابة
/// الحرة</b> هي طريق النجاة: راكة لسه مازامنتش، أو حاوية جديدة
/// وصلت النهاردة، لازم يفضلوا ممكنين. لو الليستة بقت شرط، أول راكة
/// أوفلاين كانت هتوقف الشغل.
/// </summary>
public sealed record ContainersResponse(
    IReadOnlyList<ContainerRow> Containers,
    DateTime ServerTimeUtc);

/// <summary>
/// أمر صيانة نازل من السيرفر للراكة.
///
/// <para>🔴 <b>وده مش الحمولة الكاملة.</b> الراكة صاحبة شغل الصيانة
/// (الحالة، التوقيتات، القطع، الأعطال) — والسيرفر صاحب الإسناد
/// والموافقة. التغذية دي بتنقل <b>اللي السيرفر صاحبه</b> ومعاه
/// القدر اللي يخلّي الأمر يتعرض على شاشة راكة ماشافتش الجهاز ده
/// قبل كده.</para>
///
/// <para>⚠️ ومفيش ملاحظات الفني ولا تشخيصه هنا: دي بتتكتب على
/// الراكة وبتترفع لفوق. لو نزلت تاني كانت هتدهس شغل لسه
/// مارفعش.</para>
/// </summary>
/// <param name="DeviceName">
/// ⚠️ الاسم التجاري لو موجود، وإلا الموديل الخام — نفس ترتيب
/// الشاشات، فالفني بيشوف نفس الاسم على الراكة وعلى الموقع.
/// </param>
/// <param name="Approval">
/// 🔴 <b>موافقة المحاسب — والسيرفر بيملكها زي الإسناد بالظبط.</b>
/// الراكة بتستقبلها ومابتبعتهاش؛ لو كانت بتبعتها، راكة كانت تقدر
/// توافق على أوامرها بنفسها.
/// </param>
/// <param name="ApprovalNote">
/// 🔴 <b>بينزل عشان الفني يقرا «ليه».</b> منع من غير سبب بيخلّي
/// الفني يفتكر إن البرنامج بايظ ويحاول تاني — ومنع بيتشرح بيخلّيه
/// يروح للمحاسب.
/// </param>
/// <param name="DeviceManufacturer">
/// 🔴 <b>كانت بتتشال خالص</b> — الموجز بيركّب اسم الجهاز من الموديل
/// بس، فالراكة <b>مكانتش بتعرف ماركة اللاب أصلاً</b>، وقاعدة «الفني
/// ده لماركات معيّنة» مستحيلة من غيرها.
///
/// <para>⚠️ وخام زي ما هو (<c>Hewlett-Packard</c> مش <c>HP</c>) —
/// التوحيد بقاعدة مشتركة في الطرفين مش بتخمين هنا.</para>
/// </param>
public sealed record AssignedRepairRow(
    Guid Id,
    string PublicCode,
    Guid DeviceId,
    string DeviceCode,
    string DeviceName,
    int Status,
    Guid? AssignedTechnicianId,
    string AssignedTechnicianName,
    string OpenedByName,
    DateTime OpenedAtUtc,
    string FaultSummary,
    int Approval,
    string ApprovalNote,
    string ApprovedByName,
    DateTime? ApprovalDecidedAtUtc,
    string DeviceManufacturer,
    DateTime UpdatedAtUtc);

/// <param name="HighWaterUtc">
/// 🔴 <b>العلامة بتتاخد من <u>آخر صف</u> مش من ساعة السيرفر.</b>
///
/// <para>لو أخدناها من الساعة، صف اتكتب في نفس الجزء من الثانية بعد
/// ما بنينا الرد كان هيتفوّت <b>للأبد</b> — والراكة مش هتعرف إنها
/// فوّتته.</para>
///
/// <para>⚠️ وعلى صفحة فاضية بترجع العلامة اللي الراكة بعتتها
/// (<c>null</c> لو مابعتتش) — <b>مش</b> ساعة السيرفر. ساعة السيرفر
/// هنا كانت بتدّي لراكة جديدة علامة ماكسبتهاش وتخلّيها تتخطّى
/// تاريخها كله.</para>
/// </param>
/// <param name="ApprovalEnforced">
/// 🔴 <b>العلم على المظروف مش على الصف — وده مقصود.</b>
///
/// <para>راكة جديدة بتكلّم سيرفر قديم مش عارف الموافقة هتلاقي
/// الخانة ناقصة في كل صف، و<c>0</c> معناها «معلّق» — يعني الورشة
/// كلها بتتقفل بسبب سيرفر مش عارف الميزة أصلاً. والعلم على الصف
/// مابيحلّهاش لأن دفعة فاضية مافيهاش صفوف.</para>
///
/// <para>⚠️ والراكة بتطبّق الانتظار <b>بشرطين</b>: السيرفر قال إنه
/// بيطبّق، والصف قال إنه معلّق. أي واحد ناقص = تشتغل عادي. غياب
/// الدليل على الميزة مش دليل على وجودها.</para>
/// </param>
public sealed record AssignedRepairsResponse(
    IReadOnlyList<AssignedRepairRow> Items,
    DateTime? HighWaterUtc,
    bool ApprovalEnforced,
    bool HasMore,
    DateTime ServerTimeUtc);
