using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Technicians;

public static class TechnicianErrors
{
    /// <summary>
    /// ⚠️ <b>«مش موجود» معناها «مفيش ولا فحص بالكود ده في كل
    /// التاريخ»</b> — مش «مفيش فحص في المدى المختار». اختيار أسبوع
    /// فاضي مالوش يخلّي الفني يختفي.
    /// </summary>
    public static readonly Error NotFound =
        new("technician.not_found", "الفني مش موجود.", 404);
}
