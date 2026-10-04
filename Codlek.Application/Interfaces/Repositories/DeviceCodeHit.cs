namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// لاب طلع من البحث بكود — <b>صف خام</b>.
///
/// <para>⚠️ ومنفصل عن عقد الواجهة عن قصد: المستودع بيرجّع صفوف،
/// والمعالج هو اللي بيبني العقد ويحسب «فيه تعارض؟».</para>
/// </summary>
public sealed record DeviceCodeHit(Guid DeviceId, string PublicCode, bool IsCurrent);
