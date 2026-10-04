using Codlek.Application.Contracts.Containers;
using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

public interface IContainerRepository
{
    /// <summary>
    /// القايمة، مع بحث اختياري على <b>الشكل المطبَّع</b>.
    ///
    /// <para>⚠️ <b>الموقوفة بترجع برضه، بعلامة.</b> لابات كتير بتشاور
    /// على حاوية اتوقفت، ولو اختفت من القايمة اسمها كان هيبان فاضي من
    /// غير أي تفسير — نفس قرار الأقسام.</para>
    /// </summary>
    Task<IReadOnlyList<ContainerListItem>> ListAsync(
        Guid tenantId, string? search, CancellationToken ct = default);

    Task<ImportContainer?> FindAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// 🔴 <b>الفحص ده مش بديل لفهرس التفرّد.</b>
    ///
    /// <para>هو عشان رسالة عربية مفهومة بدل استثناء قاعدة بيانات.
    /// والفهرس هو اللي بيمنع السباق بين طلبين في نفس اللحظة.</para>
    /// </summary>
    Task<bool> CodeTakenAsync(
        Guid tenantId, string normalizedCode, CancellationToken ct = default);

    void Add(ImportContainer container);

    Task<int> CountDevicesAsync(
        Guid tenantId, Guid containerId, CancellationToken ct = default);

    /// <summary>
    /// محتوى الحاوية.
    ///
    /// <para>⚠️ <b>المدموج مستبعد.</b> صفه بيفضل للتاريخ، بس عدّه في
    /// محتوى الحاوية بيقول ١٠ لابات والحقيقة ٧.</para>
    /// </summary>
    Task<IReadOnlyList<ContainerDeviceItem>> DevicesAsync(
        Guid tenantId, Guid containerId, CancellationToken ct = default);
}
