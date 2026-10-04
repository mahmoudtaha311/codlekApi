using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Reports;

public static class ReportErrors
{
    public static readonly Error NotFound =
        new("report.not_found", "الفحص مش موجود.", 404);

    /// <summary>
    /// 🔴 <b>الفني مالوش يشوف فحص حد تاني.</b>
    ///
    /// <para>والترتيب مهم: الصف بيتقرا الأول (عشان نعرف بتاع مين)
    /// وبعدين الحارس — فالمعرّف المش موجود بياخد <c>404</c>،
    /// والموجود بتاع غيره بياخد <c>403</c>. وده منقول زي ما هو:
    /// توحيدهم كان بيخبّي الفرق على اللي بيقرا السجل.</para>
    /// </summary>
    public static readonly Error NotYours =
        new("report.not_yours", "الفحص ده مش بتاعك.", 403);
}
