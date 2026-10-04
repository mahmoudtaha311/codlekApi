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
}
