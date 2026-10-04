namespace Codlek.Application.Contracts.Workflow;

/// <summary>
/// نتيجة دفعة حركات.
///
/// <para>🔴 <c>Moved</c> إما العدد كله إما <b>صفر</b> — مفيش نص
/// دفعة. لو ٣٩ من ٤٠ نجحوا، الشحنة ناقصة والسجل بيقول إنها
/// تمّت.</para>
/// </summary>
public sealed record BatchMoveResult(int Moved, string? Error)
{
    public bool Ok => Error is null;

    public static BatchMoveResult Fail(string error) => new(0, error);

    public static BatchMoveResult Recorded(int moved) => new(moved, null);
}
