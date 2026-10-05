using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>حالة جهاز — لإعادة حساب «مشكوك إنه مكرر».</summary>
public sealed record DeviceStatusRow(Guid Id, string PublicCode, DeviceLifecycleStatus Status);
