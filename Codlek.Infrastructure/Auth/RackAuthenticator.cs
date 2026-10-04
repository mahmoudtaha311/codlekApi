using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Racks;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// تحقق مفتاح المحطة — <b>بادئة في SQL، وبصمة على اللي فاضل</b>.
/// </summary>
public sealed class RackAuthenticator(IRackRepository racks, IRackKeys keys)
    : IRackAuthenticator
{
    public async Task<Rack?> AuthenticateAsync(
        string? apiKey, CancellationToken ct = default)
    {
        /*
          🔴 **الرفض قبل القاعدة.**

          مفتاح فاضي أو من حرفين مش مفتاح، وتمريره للقاعدة معناه
          استعلام على كل طلب عابر — ومسار الراكة مفتوح على الإنترنت.
        */
        if (!RackKey.Usable(apiKey)) return null;

        string key = RackKey.Clean(apiKey);

        var candidates = await racks.ActiveByKeyPrefixAsync(RackKey.Prefix(key), ct);

        /*
          ⚠️ **التحقق على كل مرشّح لحد ما واحد ينفع.**

          البادئة بتقلّل المرشّحين لواحد عملياً، بس التصادم ممكن —
          فالحلقة بتعدّي عليهم كلهم. وتوقيت الحلقة مش سر: عدد
          المرشّحين مابيعتمدش على المفتاح الصح، هو بيعتمد على
          البادئة اللي اللي بيحاول بعتها أصلاً.
        */
        foreach (var rack in candidates)
        {
            if (keys.Verify(key, rack.ApiKeyHash, rack.Salt)) return rack;
        }

        return null;
    }
}
