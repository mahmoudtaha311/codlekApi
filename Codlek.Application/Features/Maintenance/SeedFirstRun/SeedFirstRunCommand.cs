using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using MediatR;

namespace Codlek.Application.Features.Maintenance.SeedFirstRun;

/// <summary>
/// أول تشغيل على قاعدة <b>مافيهاش ولا شركة</b>: شركة <c>CODLEK</c> وحساب
/// مالك <c>admin / admin</c> لازم يغيّر الباسورد.
///
/// <para>🔴 <b>الشرط الوحيد «مفيش شركة خالص».</b> القديم كمان كان بيعمل
/// <c>admin</c> لو الشركة مالهاش مالك (<c>DbSeeder.cs:35-58</c>) — وده
/// <b>ماتنقلش عن قصد</b>: الجديد شغّال على نسخة من الإنتاج، ومالك اتشال
/// أو اتغيّرت صلاحيته بالغلط كان هيفتح حساب بباسورد معروف للكل على قاعدة
/// حقيقية. على قاعدة فاضية بس ده آمن.</para>
/// </summary>
public sealed record SeedFirstRunCommand : IRequest<Result<FirstRunSeedResult>>;
