using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetDashboard;

/// <summary>
/// اللوحة الرئيسية.
///
/// <para>⚠️ <b>بلا سياسة — الفني بيشوفها، مضيّقة على شغله.</b> ده
/// سلوك القديم: الفني بيفتح نفس الصفحة وبيشوف أرقامه هو.</para>
/// </summary>
public sealed record GetDashboardQuery(string? Range, DateTime? From, DateTime? To)
    : IRequest<Result<DashboardResponse>>;
