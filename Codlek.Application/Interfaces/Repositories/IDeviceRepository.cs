using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// الأجهزة.
///
/// <para>🔴 <b>الفلتر مكتوب مرة واحدة — القايمة والتصدير بيبنيوا
/// نفس الكائن.</b> في القديم كان مكتوب مرتين بنفس النص، ومكتوب في
/// التعليق هناك إن «لو اتغيّرت في واحد لازم تتغيّر في التاني» —
/// والتلات فلاتر الأخيرة (التحذيرات والتسليم والجهة) كانوا <b>ناقصين
/// من التصدير</b> فعلاً.</para>
/// </summary>
public interface IDeviceRepository
{
    /// <summary>صفحة من القايمة + العدد الكلي قبل التصفيح.</summary>
    Task<(IReadOnlyList<DeviceListRow> Rows, int TotalItems)> ListAsync(
        Guid tenantId, DeviceListFilter filter, CancellationToken ct = default);

    /// <summary>
    /// نفس الفلتر، <b>بلا تصفيح</b> — للتصدير.
    /// </summary>
    /// <param name="cap">
    /// ⚠️ سقف على الصفوف. والقص بيتقال في الملف نفسه — ملف مقصوص
    /// في صمت بيتقري على إنه كل البيانات.
    /// </param>
    Task<IReadOnlyList<DeviceExportRow>> ExportAsync(
        Guid tenantId, DeviceListFilter filter, int cap, CancellationToken ct = default);

    /// <summary>
    /// لاب بكوده — <b>للبحث السريع في الترويسة</b>.
    ///
    /// <para>⚠️ بيدوّر في المدموجين كمان: الكود المتقاعد مطبوع على
    /// ليبل ملزوق على لاب حقيقي.</para>
    /// </summary>
    Task<Device?> FindByCodeAsync(
        Guid tenantId, string publicCode, CancellationToken ct = default);

    /// <summary>أسماء الجهات اللي في الصفحة — قراية واحدة.</summary>
    Task<IReadOnlyDictionary<Guid, string>> LocationNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>أسماء وأكواد الفنيين الحائزين — قراية واحدة.</summary>
    Task<IReadOnlyDictionary<Guid, TechnicianLabel>> HolderNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>أكواد الراكات — قراية واحدة.</summary>
    Task<IReadOnlyDictionary<Guid, string>> RackCodesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>
    /// مكان كل راكة — <b>«اتفحص في أنهي دور»</b>.
    ///
    /// <para>⚠️ وده سؤال مختلف عن «هو فين دلوقتي»: اللاب ممكن يكون
    /// اتفحص في الدور التاني وهو دلوقتي في مخزن المبيعات.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> RackLocationsAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);
}
