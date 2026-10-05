namespace Codlek.Application.Contracts.Maintenance;

/// <summary>
/// نتيجة بذرة أول تشغيل.
/// </summary>
/// <param name="Seeded">
/// <c>false</c> = القاعدة فيها شركة أصلاً ومااتعملش حاجة — وده الطبيعي
/// في كل إقلاع بعد الأول.
/// </param>
/// <param name="TenantId">الشركة اللي اتعملت، لو اتعملت.</param>
public sealed record FirstRunSeedResult(bool Seeded, Guid? TenantId);
