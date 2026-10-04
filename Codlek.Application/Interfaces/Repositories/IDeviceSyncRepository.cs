using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// استقبال الأجهزة الجاية من الراكة.
///
/// <para>🔴 <b>والراكة هي اللي بتولّد المعرّف.</b> الفني ماسك اللاب
/// على بنش من غير نت، فالجهاز لازم يتعمل هناك دلوقتي. السيرفر بيعمل
/// <c>upsert</c> بالمعرّف ده — لو ولّد واحد جديد، كل مزامنة كانت
/// هتخلّق <b>نسخة تانية من نفس اللاب</b>.</para>
///
/// <para>🔴 <b>والصفوف متتبّعة، والحفظ من المنادي</b> — عشان الجهاز
/// والفحص اللي بيشاور عليه يوصلوا <b>مع بعض</b>.</para>
/// </summary>
public interface IDeviceSyncRepository
{
    /// <summary>
    /// الأجهزة اللي المرساة دي بتشاور عليها — <b>والمدموجين
    /// مستبعدين</b>.
    ///
    /// <para>🔴 <b>واستبعاد المدموجين مش تحسين.</b> الجهاز اللي اتدمج
    /// بيفضل صفه كشاهد قبر ومراسيه بتفضل نشطة عليه، فهو بيطابق
    /// الكانوني في كل استعلام. اللي اتشاف في الإنتاج: المدير بيدمج
    /// الجهازين، وأول مزامنة بترجّع الكانوني «يُشتبه أنه مكرر» تاني
    /// — <b>بيشتبه في نفسه</b>، لأن التوأم الوحيد هو شاهد دمجه
    /// هو.</para>
    /// </summary>
    Task<IReadOnlyList<Guid>> MatchAnchorAsync(
        Guid tenantId, DeviceIdentifierKind kind, string normalizedValue,
        Guid? exceptDeviceId, int take, CancellationToken ct = default);

    /// <summary>الاسم المستعار للمعرّف ده — <b>متتبّع</b>.</summary>
    Task<DeviceAlias?> FindAliasAsync(
        Guid tenantId, Guid aliasDeviceId, CancellationToken ct = default);

    /// <summary>الجهاز بمراسيه — <b>متتبّع</b>.</summary>
    Task<Device?> FindWithIdentifiersAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// فيه جهاز <b>تاني</b> شايل الكود ده؟
    ///
    /// <para>⚠️ والاستثناء بالمعرّف الكانوني — فنفس اللاب بيستثني
    /// نفسه.</para>
    /// </summary>
    Task<Guid?> CodeClashAsync(
        Guid tenantId, string publicCode, Guid exceptDeviceId,
        CancellationToken ct = default);

    void Add(Device device);

    void AddAlias(DeviceAlias alias);

    /// <summary>
    /// ⚠️ <b>حركة سجل</b> — بتتستعمل لما كود جهاز يتغيّر بعد التعرّف
    /// على هويته.
    /// </summary>
    void AddWorkflowEvent(DeviceWorkflowEvent workflowEvent);

    Task<ImportContainer?> FindContainerAsync(
        Guid tenantId, string normalizedCode, CancellationToken ct = default);

    void AddContainer(ImportContainer container);

    /// <summary>
    /// أجهزة تانية شايلة أي مرساة من دي — <b>والمدموجين
    /// مستبعدين</b>.
    /// </summary>
    Task<IReadOnlyList<Guid>> TwinsByAnchorsAsync(
        Guid tenantId, Guid exceptDeviceId, IReadOnlyCollection<string> normalizedValues,
        CancellationToken ct = default);

    /// <summary>الأجهزة الشغّالة من دي — <b>متتبّعة</b>، عشان تتعلّم.</summary>
    Task<IReadOnlyList<Device>> ActiveDevicesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// الصف ده اتغيّر فيه حاجة فعلاً؟
    ///
    /// <para>🔴 <b>والسؤال ده عند المستودع لأنه بيعرف المتتبّع.</b>
    /// الفرق بين «اتحدّث» و«زي ما هو» بيوصل الراكة في
    /// <c>status</c>، والراكة بتعدّه. ولو قلنا «اتحدّث» على جهاز
    /// ماتغيّرش، العدّاد اللي المدير بيقيس بيه النشاط <b>بيبقى
    /// وهم</b>.</para>
    /// </summary>
    bool WasTouched(Device device);
}
