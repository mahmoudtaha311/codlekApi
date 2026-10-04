namespace Codlek.Application.Abstractions;

/// <summary>رفض التجديد — <b>سبب واحد للزبون</b>.</summary>
public static class RefreshErrors
{
    /*
      ⚠️ **رسالة واحدة لكل الأسباب، عن قصد.**

      لو الرد قال «التوكن ده اتستعمل قبل كده» أو «الحساب موقوف»، اللي
      بيجرّب بياخد منها معلومة. السبب الحقيقي (<c>RefreshOutcome</c>)
      بيروح للسجل وبس.
    */
    public static readonly Error Rejected =
        new("auth.refresh_rejected", "الجلسة انتهت — سجّل دخول تاني.", 401);
}
