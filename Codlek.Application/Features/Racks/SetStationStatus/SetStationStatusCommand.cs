using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Racks;
using MediatR;

namespace Codlek.Application.Features.Racks.SetStationStatus;

/// <summary>
/// إيقاف محطة أو إرجاعها.
///
/// <para>🔴 <b>أمر واحد لمسارين</b> — <c>/suspend</c>
/// و<c>/activate</c>. القاعدة اللي بينهم واحدة (الملغية نهائياً
/// مابترجعش)، ونسختين منها كانت أول حاجة تروح تختلف: القديم كان
/// فيه نسخة في النقطة ونسخة في صفحة Razor، والتانية ماكانتش بتفحص
/// الإلغاء خالص.</para>
///
/// <para>⚠️ و<see cref="Resume"/> <c>bool</c> مش
/// <c>RackStatus</c>: الأمر بيعرف حالتين بس، وقبول الـenum كامل
/// كان معناه إن كنترولر يقدر يبعت <c>Revoked</c> ويعدّي على فحص
/// السبب.</para>
/// </summary>
public sealed record SetStationStatusCommand(Guid Id, bool Resume)
    : IRequest<Result<RackActionResponse>>;
