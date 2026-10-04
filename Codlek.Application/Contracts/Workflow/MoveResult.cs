using Codlek.Core.Entities;

namespace Codlek.Application.Contracts.Workflow;

/// <summary>
/// ناتج تسجيل حركة.
///
/// <para>⚠️ <b>الحركة المكرّرة بترجع <c>Ok</c> ومعاها الصف القديم</b>
/// — مش خطأ. الراكة بتعيد الرفع بعد انقطاع، والرفض ساعتها بيخلّيها
/// تعيد للأبد.</para>
/// </summary>
public sealed record MoveResult(DeviceWorkflowEvent? Event, string? Error)
{
    public bool Ok => Error is null;

    public static MoveResult Fail(string error) => new(null, error);

    public static MoveResult Recorded(DeviceWorkflowEvent row) => new(row, null);
}
