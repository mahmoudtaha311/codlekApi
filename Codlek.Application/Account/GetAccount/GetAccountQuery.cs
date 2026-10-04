using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Account;
using MediatR;

namespace Codlek.Application.Account.GetAccount;

/// <summary>
/// بيانات الحساب الحالي.
///
/// <para>🔴 <b>مفيش <c>userId</c> في الاستعلام.</b> المستخدم بييجي من
/// التوكن. أول ما معرّف مستخدم يُقبل من العميل، الفرق بين «شوف حسابي»
/// و«شوف حساب المالك» بيبقى سطر تحقق واحد.</para>
/// </summary>
public sealed record GetAccountQuery : IRequest<Result<AccountResponse>>;
