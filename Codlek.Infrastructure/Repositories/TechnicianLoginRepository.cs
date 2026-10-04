using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Repositories;

/// <inheritdoc cref="ITechnicianLoginRepository"/>
public sealed class TechnicianLoginRepository(AppDbContext db) : ITechnicianLoginRepository
{
    /// <summary>
    /// ⚠️ <b>والفلتر الزمني <c>&gt;=</c> مش <c>&gt;</c></b> — نفس
    /// القديم. الفرق جزء من ثانية، بس تثبيته بيخلّي الفحص يقيس
    /// النافذة بدقة بدل ما يسيبها تعوم.
    /// </summary>
    public Task<int> RecentFailuresAsync(
        Guid rackId, string normalizedUsername, DateTime sinceUtc,
        CancellationToken ct = default) =>
        db.TechnicianLoginAttempts
            .Where(a => a.RackId == rackId
                     && a.AttemptedUsername == normalizedUsername
                     && !a.Success
                     && a.AtUtc >= sinceUtc)
            .CountAsync(ct);

    /// <summary>
    /// 🔴 <b>مفيش <c>AsNoTracking</c> هنا عن قصد.</b> الدخول الناجح
    /// بيكتب على الصف، وتغيير الباسورد بيكتب البصمة والنسخة.
    /// </summary>
    public Task<Technician?> FindByLoginKeyAsync(
        Guid tenantId, string normalizedUsername, CancellationToken ct = default) =>
        db.Technicians.FirstOrDefaultAsync(
            t => t.TenantId == tenantId && t.NormalizedUsername == normalizedUsername, ct);

    public void AddAttempt(TechnicianLoginAttempt attempt) =>
        db.TechnicianLoginAttempts.Add(attempt);
}
