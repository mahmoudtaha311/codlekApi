using Codlek.Application.Abstractions;
using MediatR;

namespace Codlek.Application.Features.Maintenance.SeedHandoverLocations;

/// <summary>
/// بيرجّع أي جهة من جهات التسليم الأربعة ناقصة من الشركة — بالكود.
///
/// <para>🔴 <b>من غيرها شاشة التسليم مالهاش حاجة تعرضها.</b> قاعدة
/// جديدة أو صف اتمسح بالإيد = مدير الدور مايقدرش يسلّم لاب لحد.</para>
///
/// <para>⚠️ <b>ومابيلمسش الموجود.</b> الاسم أو الترتيب اللي حد غيّره
/// قراره هو، والموقع الموقوف بيفضل موقوف — نفس القديم
/// (<c>DbSeeder.cs:82-112</c>).</para>
///
/// <para>⚠️ <b>فرق واحد عن القديم:</b> القديم بيبذر أول شركة يلاقيها
/// بس (<c>FirstOrDefaultAsync</c> من غير ترتيب)، وهنا كل شركة. على
/// الإنتاج فيه شركة واحدة فالنتيجة واحدة؛ ومع أكتر من شركة «أول واحدة»
/// كانت عشوائية — وشركة من غير جهات تسليم مش هتقدر تسلّم خالص.</para>
/// </summary>
/// <returns>عدد الجهات اللي اتضافت.</returns>
public sealed record SeedHandoverLocationsCommand(Guid TenantId) : IRequest<Result<int>>;
