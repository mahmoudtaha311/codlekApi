namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// نتيجة مسح كود.
///
/// <para>🔴 <b>وأكتر من لاب بنفس الكود = استيكر اتنقل.</b> بنرجّعهم
/// كلهم ونقول فيه تعارض، بدل ما نختار واحد في صمت — اللي بيمسح هو
/// اللي يعرف أنهي لاب في إيده.</para>
///
/// <para>⚠️ و<see cref="DeviceId"/> و<see cref="IsCurrent"/>
/// بيتكرروا من أول عنصر في <see cref="Matches"/> عن قصد: الحالة
/// العادية (تطابق واحد) الواجهة بتقراهم على طول من غير ما تلف على
/// قايمة.</para>
/// </summary>
public sealed record DeviceLookupResult(
    Guid DeviceId,
    string PublicCode,
    bool IsCurrent,
    bool Conflict,
    IReadOnlyList<DeviceCodeMatch> Matches);
