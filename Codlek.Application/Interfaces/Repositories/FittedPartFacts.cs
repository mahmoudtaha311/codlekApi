namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// قطعة اتركّبت في أمر صيانة — <b>خام، قبل التجميع</b>.
///
/// <para>🔴 <b>التجميع بالاسم بيحصل في الـHandler بعد
/// التطبيع.</b> الفني بيكتب «شاشة» و«شاشه» و«شاشة  » — ومن غير
/// التطبيع دي تلات صفوف وكل واحد رقمه صغير، فأكتر قطعة بتختفي في
/// التفتيت.</para>
///
/// <para>⚠️ والتطبيع مالوش ترجمة لـSQL، فحطّه في <c>GroupBy</c> كان
/// بيخلّي EF يجيب الصفوف كلها <b>في صمت</b>. الجلب هنا مقصود
/// ومحدود بالفترة.</para>
/// </summary>
public sealed record FittedPartFacts(
    string Name,
    string InventoryCode,
    int Quantity,
    Guid? DeviceId);
