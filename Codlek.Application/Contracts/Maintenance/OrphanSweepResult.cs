namespace Codlek.Application.Contracts.Maintenance;

/// <summary>
/// نتيجة لفّة ربط الفحوص اليتيمة — نفس عدّادات القديم بالحرف.
/// </summary>
/// <param name="Scanned">كل فحص من غير جهاز اتعاد تقييمه.</param>
/// <param name="Linked">اتربط بجهاز موجود.</param>
/// <param name="Flagged">
/// مالقاش تطابق ومستني المدير — <b>بيشمل</b> اللي كان متعلّم قبل كده.
/// </param>
public sealed record OrphanSweepResult(int Scanned, int Linked, int Flagged);
