namespace Codlek.Application.Interfaces;

/// <summary>
/// بيحوّل معرّف جهاز لمعرّفه الكانوني.
///
/// <para>🔴 <b>كل حاجة بتكتب على جهاز لازم تعدّي من هنا الأول.</b>
/// اللاب الواحد بيوصل للسيرفر بمعرّفات مختلفة من راكات مختلفة، والدمج
/// بيخلّي واحد منهم هو الكانوني.</para>
///
/// <para>🔴 <b>والبحث المباشر مكان ده كان سبب عطل حقيقي:</b> حركات
/// اترفضت لجهاز <b>وصل فعلاً</b> بس بمعرّف تاني — والفني شاف «الجهاز
/// مش موجود» على لاب هو ماسكه في إيده.</para>
/// </summary>
public interface IDeviceReference
{
    /// <summary>
    /// المعرّف الكانوني، أو <c>null</c>.
    /// </summary>
    /// <returns>
    /// <c>null</c> في تلات حالات: المعرّف فاضي، أو مش موجود ولا
    /// كاسم مستعار، أو السلسلة فيها حلقة / أطول من
    /// <c>DeviceMergeWalk.MaxHops</c>.
    ///
    /// <para>⚠️ والتلاتة بيتعاملوا بنفس الطريقة عند المنادي («الجهاز
    /// مش موجود») — بس اللي بيقرا السجل محتاج يفرّق، فالتنفيذ بيسجّل
    /// الفرق.</para>
    /// </returns>
    Task<Guid?> ResolveAsync(Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// نفس الحاجة لمجموعة معرّفات — <b>بعدد استعلامات ثابت</b>.
    ///
    /// <para>⚠️ مسار المزامنة بيوصل بمية جهاز في الدفعة. استعلام لكل
    /// واحد معناه مية رحلة للقاعدة، والدفعة بتتوقّت.</para>
    /// </summary>
    /// <returns>
    /// خريطة «المعرّف اللي اتبعت ← الكانوني». <b>المعرّفات اللي
    /// مااتحلّتش مش في الخريطة خالص</b> — مش موجودة بقيمة
    /// <c>null</c>.
    /// </returns>
    Task<IReadOnlyDictionary<Guid, Guid>> ResolveManyAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default);
}
