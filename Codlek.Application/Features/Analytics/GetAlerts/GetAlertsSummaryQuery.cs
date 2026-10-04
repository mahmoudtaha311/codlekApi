using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetAlerts;

/// <summary>
/// عدّادات التحذيرات لترويسة اللوحة.
///
/// <para>⚠️ <b>بلا فترة بقصد</b> — التحذير حالة قايمة دلوقتي.</para>
/// </summary>
public sealed record GetAlertsSummaryQuery : IRequest<Result<AlertsSummary>>;
