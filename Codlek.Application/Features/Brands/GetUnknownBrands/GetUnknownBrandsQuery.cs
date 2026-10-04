using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Brands;
using MediatR;

namespace Codlek.Application.Features.Brands.GetUnknownBrands;

/// <summary>
/// الأسماء اللي في الأجهزة ومالهاش ماركة.
///
/// <para>🔴 <b>دي الشاشة اللي بتخلّي «المش معروفة بتعدّي» قرار
/// آمن.</b> من غيرها، لاب بماركة جديدة بيعدّي من القيد في صمت للأبد —
/// وصاحب الشغل مش هيعرف إن فيه ماركة ناقصة إلا بالصدفة.</para>
/// </summary>
public sealed record GetUnknownBrandsQuery : IRequest<Result<IReadOnlyList<UnknownBrandRow>>>;
