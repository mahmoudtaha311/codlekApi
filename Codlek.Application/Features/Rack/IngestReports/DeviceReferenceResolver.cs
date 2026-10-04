using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;

namespace Codlek.Application.Features.Rack.IngestReports;

/// <summary>
/// بيترجم معرّف جهاز جاي من راكة <b>للجهاز الكانوني الحيّ</b>.
///
/// <para>🔴 <b>وده موجود عشان حادثة إنتاج حقيقية</b> — اقرا
/// <see cref="DeviceMergeChain"/>.</para>
///
/// <para>⚠️ <b>واللفّة بالجملة:</b> كل دورة بتحلّ <b>طبقة</b> من
/// الأسامي المستعارة والدمج لكل المعرّفات مع بعض، وبتوقف أول ما
/// مفيش حاجة لسه محتاجة تتبع. الحالة الشايعة (كله أجهزة حيّة)
/// بتخلص في <b>استعلام واحد</b>؛ المدموجين بياخدوا لفّة لكل خطوة،
/// وده نادر وقصير.</para>
/// </summary>
public sealed class DeviceReferenceResolver(IDeviceReferenceRepository devices)
{
    /// <summary>
    /// خريطة من المعرّف اللي وصل للكانوني الحيّ.
    ///
    /// <para>⚠️ <b>المعرّف اللي مالوش ترجمة مابيظهرش في الخريطة
    /// خالص</b> — وده معناه «السيرفر فعلاً عمره ما شافه»، مش «شافه
    /// وماعرفش يترجمه».</para>
    /// </summary>
    public async Task<Dictionary<Guid, Guid>> ResolveManyAsync(
        Guid tenantId, IReadOnlyCollection<Guid> deviceIds, CancellationToken ct = default)
    {
        var map = new Dictionary<Guid, Guid>();

        var wanted = deviceIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (wanted.Count == 0) return map;

        // الأصلي ← اللي بندوّر عليه دلوقتي.
        var pending = wanted.ToDictionary(id => id, id => id);

        // ⚠️ وسلسلة كل معرّف لوحده — عشان حلقة في سلسلة ماتوقّفش
        //    الباقي.
        var chains = wanted.ToDictionary(id => id, id => new HashSet<Guid> { id });

        for (int hop = 0; hop < DeviceMergeChain.MaxHops && pending.Count > 0; hop++)
        {
            var probe = pending.Values.Distinct().ToList();

            var rows = await devices.DeviceStatesAsync(tenantId, probe, ct);

            var missing = probe.Where(id => !rows.ContainsKey(id)).ToList();

            // ⚠️ الأسامي المستعارة بتتسأل بس على اللي مالوش صف —
            //    الجهاز الحيّ مالوش لازمة لفّة تانية.
            var aliases = missing.Count == 0
                ? new Dictionary<Guid, Guid>()
                : await devices.AliasTargetsAsync(tenantId, missing, ct);

            var next = new Dictionary<Guid, Guid>();

            foreach (var (original, probing) in pending)
            {
                Guid? follow = null;

                if (rows.TryGetValue(probing, out var row))
                {
                    /*
                      🔴 **شاهد قبر: الصف موجود بس الجهاز اتدمج في
                      غيره.** «موجود» مش كفاية — لازم نكمّل للحيّ.
                    */
                    if (row.IsTombstone)
                    {
                        follow = row.MergedIntoDeviceId;
                    }
                    else
                    {
                        // جهاز حيّ — خلاص.
                        map[original] = probing;
                        continue;
                    }
                }
                else if (aliases.TryGetValue(probing, out var canonical))
                {
                    follow = canonical;
                }

                // ⚠️ مالوش صف ولا اسم مستعار ← مجهول، مابيدخلش
                //    الخريطة.
                if (follow is not { } step) continue;

                // ⚠️ حلقة مقفولة ← نسيبه مجهول بدل ما نلف للأبد.
                if (!chains[original].Add(step)) continue;

                next[original] = step;
            }

            pending = next;
        }

        return map;
    }

    /// <summary>معرّف واحد.</summary>
    public async Task<Guid?> ResolveAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default)
    {
        var map = await ResolveManyAsync(tenantId, [deviceId], ct);

        return map.TryGetValue(deviceId, out var canonical) ? canonical : null;
    }
}
