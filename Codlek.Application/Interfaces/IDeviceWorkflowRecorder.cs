using Codlek.Application.Contracts.Workflow;

namespace Codlek.Application.Interfaces;

/// <summary>
/// بيسجّل حركة جهاز، وبيحدّث الكاش عليه.
///
/// <para>🔴 <b>الكاش بيتحدّث هنا وبس — جنب الحدث اللي بيفسّره.</b>
/// <c>Device.OperationalStage</c> و<c>CurrentHolderTechnicianId</c> و
/// <c>CurrentLocationId</c> كلهم ملخّص لآخر حركة. ولو حد حدّثهم من
/// مكان تاني، الجهاز بيقول «في الصيانة» ومفيش ولا حركة بتفسّر
/// إزاي.</para>
///
/// <para>⚠️ <b>ومابيحفظش.</b> الحفظ مسؤولية <see cref="IUnitOfWork"/> —
/// عشان الحركة والتغيير اللي سبّبها ينزلوا في حفظة واحدة.</para>
/// </summary>
public interface IDeviceWorkflowRecorder
{
    Task<MoveResult> RecordAsync(
        Guid tenantId, WorkflowMove move, CancellationToken ct = default);

    /// <summary>
    /// دفعة حركات — <b>الكل أو ولا واحد</b>.
    ///
    /// <para>🔴 <b>السبب اللي الدالة دي موجودة عشانه:</b> لو ٣٩ من
    /// ٤٠ لاب نجحوا، الشحنة ناقصة <b>والسجل بيقول إنها تمّت</b>. فأول
    /// فشل بيوقّف الدفعة كلها ويرجّع صفر.</para>
    ///
    /// <para>⚠️ <b>ومفيش <c>SaveChanges</c> هنا كمان.</b> الحفظ
    /// مسؤولية المنادي — وده اللي بيخلّي «الكل أو ولا واحد» حقيقي:
    /// الحركات كلها في الذاكرة لحد ما كلهم ينجحوا.</para>
    /// </summary>
    /// <param name="build">
    /// ⚠️ دالة بتبني حركة لكل معرّف. التسليم بيبعت نفس الوقت ونفس
    /// الجهة لكل اللابات، فالمبني بيختلف في <c>DeviceId</c> بس.
    /// </param>
    Task<BatchMoveResult> RecordManyAsync(
        Guid tenantId,
        IReadOnlyList<Guid> deviceIds,
        Func<Guid, WorkflowMove> build,
        CancellationToken ct = default);
}
