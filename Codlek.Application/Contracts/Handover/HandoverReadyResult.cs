namespace Codlek.Application.Contracts.Handover;

/// <summary>
/// نتيجة المراجعة.
/// </summary>
/// <param name="Changed">
/// ⚠️ <b>عدد اللي <u>اتغيّر</u> فعلاً، مش اللي اتبعت.</b> اللاب
/// المعلّم خلاص مابيتعدّش تاني — الرقم ده بيوصف الكتابة مش الطلب.
/// </param>
public sealed record HandoverReadyResult(int Changed, bool Ready);
