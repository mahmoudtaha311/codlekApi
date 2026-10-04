using Codlek.Application.Abstractions;
using MediatR;

namespace Codlek.Application.Auth.Logout;

/// <summary>
/// خروج — <b>بيقفل كل جلسات الحساب</b>.
///
/// <para>⚠️ <b>مش الجلسة دي بس.</b> «خروج» عند المستخدم معناه إن
/// حسابه مابقاش مفتوح. ولو قفلنا الجهاز ده وبس، جهاز تاني نسيه مفتوح
/// يفضل شغّال أسبوع — وهو فاكر إنه خرج.</para>
/// </summary>
public sealed record LogoutCommand(Guid UserId) : IRequest<Result>;
