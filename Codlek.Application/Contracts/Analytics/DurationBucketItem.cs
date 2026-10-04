namespace Codlek.Application.Contracts.Analytics;

/// <summary>شريحة مدة — والترتيب عقد: الواجهة بترسم الأعمدة باللي بيوصلها.</summary>
public sealed record DurationBucketItem(string Key, string Label, int Count);
