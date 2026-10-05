namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// فحص من غير جهاز — <b>المعرّف والنسخة الخام وبس</b>.
///
/// <para>⚠️ <b>مش الصف كله.</b> النسخة الخام لوحدها ~٢٠ كيلوبايت للفحص
/// على الإنتاج، والاستضافة رامها قليل — تحميل الصف المتتبّع بكل أعمدته
/// كان بيضاعف الذاكرة من غير ما نحتاج ولا عمود منها.</para>
/// </summary>
public sealed record OrphanReportRow(Guid Id, string RawJson, bool NeedsDeviceResolution);
