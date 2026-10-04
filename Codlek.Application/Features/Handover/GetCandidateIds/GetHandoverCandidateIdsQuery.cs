using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Handover;
using MediatR;

namespace Codlek.Application.Features.Handover.GetCandidateIds;

/// <summary>
/// معرّفات كل اللي طلع من الفلتر — <b>لزرار «اختر الكل»</b>.
///
/// <para>🔴 <b>«ماينفعش أنزل ٣ صفحات وأعلّم في كل واحدة».</b>
/// القايمة بتجيب ٢٥ في الصفحة، فـ٥١ لاب جاهز معناهم تلات صفحات
/// وتعليم يدوي في كل واحدة — والغلطة الواحدة بتضيّع الشغل
/// كله.</para>
///
/// <para>⚠️ <b>وده مش بيلغي إن المدير لازم يشوف هو بيختار إيه.</b>
/// كان فيه اعتراض إن ده «تفويض مش اختيار» — والاعتراض صح، بس الحل
/// مش إننا نمنع: الحل إن الاختيار يبقى <b>معلوم</b>. النقطة بترجّع
/// المعرّفات والواجهة بتقول «هتختار ٥١» قبل ما يدوس، والدفعة نفسها
/// «الكل أو ولا واحد»، والسيرفر بيعيد التحقّق من أهلية كل لاب وقت
/// التسليم مهما كانت القايمة جات منين.</para>
/// </summary>
/// <param name="Take">
/// ⚠️ <b>موجود عشان السقف <u>يتقاس</u>.</b> الواجهة مابتبعتوش؛ هي
/// عايزة الدفعة كاملة لحد السقف. ومن غيره، السلوك عند تخطّي السقف
/// مايتجرّبش غير بزرع ٥٠١ جهاز في قاعدة الفحوص — وده بيبطّئ كل
/// الملفات التانية.
/// </param>
public sealed record GetHandoverCandidateIdsQuery(
    string? Search,
    Guid? Container,
    string? Review,
    int? Take) : IRequest<Result<HandoverCandidateIds>>;
