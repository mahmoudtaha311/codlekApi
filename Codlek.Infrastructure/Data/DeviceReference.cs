using Codlek.Application.Interfaces;
using Codlek.Core.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// المشي من معرّف جهاز لمعرّفه الكانوني.
///
/// <para>⚠️ القواعد (الحد الأقصى · الحلقة · المعرّف الفاضي) في
/// <see cref="DeviceMergeWalk"/> — الكلاس ده بيمشي بس.</para>
/// </summary>
public sealed class DeviceReference(AppDbContext db, ILogger<DeviceReference> log)
    : IDeviceReference
{
    public async Task<Guid?> ResolveAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default)
    {
        if (!DeviceMergeWalk.IsAskable(deviceId))
        {
            // 🔴 فاضي مش «مش موجود» — هو «الطلب كان ناقص».
            log.LogWarning("حلّ جهاز بمعرّف فاضي — الطلب اللي جايّ ناقص.");
            return null;
        }

        var seen = new HashSet<Guid>();
        Guid current = deviceId;

        for (int hop = 0; hop < DeviceMergeWalk.MaxHops; hop++)
        {
            if (!DeviceMergeWalk.CanVisit(seen, current))
            {
                // 🔴 حلقة = بيانات بايظة. لازم حد يبصّ عليها.
                log.LogError(
                    "🔴 حلقة في سلسلة دمج الأجهزة — الشركة {TenantId}، البداية {DeviceId}.",
                    tenantId, deviceId);

                return null;
            }

            var row = await db.Devices.AsNoTracking()
                .Where(d => d.Id == current && d.TenantId == tenantId)
                .Select(d => new { d.Status, d.MergedIntoDeviceId })
                .FirstOrDefaultAsync(ct);

            if (row is null)
            {
                /*
                  ⚠️ **مش موجود كجهاز؟ يبقى يمكن اسم مستعار.**

                  وده المسار اللي العطل كان فيه: الراكة بتبعت معرّفها
                  المحلي، واللاب متسجّل على السيرفر بمعرّف تاني — والصلة
                  بينهم في `DeviceAliases`.
                */
                var alias = await db.DeviceAliases.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && a.AliasDeviceId == current)
                    .Select(a => (Guid?)a.CanonicalDeviceId)
                    .FirstOrDefaultAsync(ct);

                if (alias is not { } next) return null;

                current = next;
                continue;
            }

            if (DeviceMergeWalk.MergedInto(row.Status, row.MergedIntoDeviceId) is { } into)
            {
                current = into;
                continue;
            }

            return current;
        }

        // 🔴 عدّى الحد الأقصى — سلسلة أطول من ١٠ معناها بيانات بايظة.
        log.LogError(
            "🔴 سلسلة دمج أطول من {MaxHops} — الشركة {TenantId}، البداية {DeviceId}.",
            DeviceMergeWalk.MaxHops, tenantId, deviceId);

        return null;
    }

    /// <summary>
    /// نفس المشي لمجموعة — <b>بعدد استعلامات ثابت</b>.
    ///
    /// <para>⚠️ كل خطوة بتسأل عن <b>كل</b> المعرّفات اللي لسه ماشية
    /// مع بعض: استعلامين لكل خطوة بدل استعلامين لكل جهاز. مسار
    /// المزامنة بيوصل بمية جهاز في الدفعة.</para>
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, Guid>> ResolveManyAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default)
    {
        var resolved = new Dictionary<Guid, Guid>();

        var wanted = deviceIds.Where(DeviceMergeWalk.IsAskable).Distinct().ToList();

        if (wanted.Count == 0) return resolved;

        // «الأصلي ← اللي بندوّر عليه دلوقتي»
        var pending = wanted.ToDictionary(id => id, id => id);

        // وسلسلة كل واحد لوحده — عشان حلقة في واحد ماتوقّفش الباقي.
        var chains = wanted.ToDictionary(id => id, id => new HashSet<Guid> { id });

        for (int hop = 0; hop < DeviceMergeWalk.MaxHops && pending.Count > 0; hop++)
        {
            var probe = pending.Values.Distinct().ToList();

            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.TenantId == tenantId && probe.Contains(d.Id))
                .Select(d => new { d.Id, d.Status, d.MergedIntoDeviceId })
                .ToDictionaryAsync(d => d.Id, ct);

            var missing = probe.Where(id => !rows.ContainsKey(id)).ToList();

            var aliases = missing.Count == 0
                ? []
                : await db.DeviceAliases.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && missing.Contains(a.AliasDeviceId))
                    .ToDictionaryAsync(a => a.AliasDeviceId, a => a.CanonicalDeviceId, ct);

            var next = new Dictionary<Guid, Guid>();

            foreach (var (original, probing) in pending)
            {
                if (rows.TryGetValue(probing, out var row))
                {
                    if (DeviceMergeWalk.MergedInto(row.Status, row.MergedIntoDeviceId)
                        is { } into)
                    {
                        if (DeviceMergeWalk.CanVisit(chains[original], into))
                            next[original] = into;

                        // ⚠️ حلقة في السلسلة دي؟ بنسيبها من غير نتيجة —
                        // والباقي بيكمّل.
                        continue;
                    }

                    resolved[original] = probing;
                    continue;
                }

                if (aliases.TryGetValue(probing, out var canonical)
                    && DeviceMergeWalk.CanVisit(chains[original], canonical))
                {
                    next[original] = canonical;
                }

                // ⚠️ ولا جهاز ولا اسم مستعار؟ مابيتحطّش في الخريطة
                // خالص — مش بقيمة فاضية.
            }

            pending = next;
        }

        return resolved;
    }
}
