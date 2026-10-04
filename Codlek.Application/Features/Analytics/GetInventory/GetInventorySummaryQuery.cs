using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetInventory;

/// <summary>
/// جرد الورشة.
///
/// <para>⚠️ <b>للمديرين وفوق</b> — ده جرد على مستوى الشركة ومالوش
/// نسخة «بتاعتي» تتعرض لفني.</para>
/// </summary>
public sealed record GetInventorySummaryQuery : IRequest<Result<InventorySummary>>;
