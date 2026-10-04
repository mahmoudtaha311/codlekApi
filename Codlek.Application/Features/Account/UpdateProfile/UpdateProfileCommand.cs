using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Account;
using MediatR;

namespace Codlek.Application.Features.Account.UpdateProfile;

/// <summary>
/// تعديل اسم العرض — <b>وبس</b>.
///
/// <para>🔴 مفيش دور ولا شركة ولا كود ولا صلاحية في الأمر ده. مش
/// «بنتأكد إنهم ماتغيّروش» — إحنا أصلاً مش بناخدهم.</para>
/// </summary>
public sealed record UpdateProfileCommand(string DisplayName)
    : IRequest<Result<AccountResponse>>;
