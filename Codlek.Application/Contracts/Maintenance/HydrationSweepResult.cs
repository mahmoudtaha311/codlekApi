namespace Codlek.Application.Contracts.Maintenance;

/// <summary>نتيجة ملء الاسم التجاري على أجهزة شركة.</summary>
/// <param name="Examined">كل جهاز اتبص عليه.</param>
/// <param name="Updated">اللي اسمه أو مصدره أو كود المصنع بتاعه اتغيّر.</param>
public sealed record HydrationSweepResult(int Examined, int Updated);
