namespace Codlek.Core.Enums;

/*
  🔴 **الأرقام دي عقد مجمّد — ممنوع تتغيّر، وممنوع يتزاد حاجة في النص.**

  الراكة (تطبيق الويندوز) بتقرا القيم دي **كأرقام على السلك**، مش
  كأسماء. يعني لو `InProgress` بقت ٣ بدل ٢، الراكة هتفهم «قيد الصيانة»
  على إنها «تمت الصيانة» — من غير أي خطأ ومن غير ما حد ياخد باله.

  ⚠️ **والزيادة في الآخر بس.** `UserRole.Accountant = 4` اتزاد في
  الآخر عن قصد لنفس السبب.

  ⚠️ وفيه فحوص بتجمّد الأرقام دي على الطرفين:
  `OperationalPayloadContractTests` على السيرفر،
  و`tools\SpicsHarness\RepairChecks.cs` على الراكة.
*/

/// <summary>
/// صلاحية حساب اللوحة.
///
/// <para>⚠️ <b>مفيش ترتيب بين الأدوار.</b> كل حاجز في النظام عضوية
/// صريحة في مجموعة، مش مقارنة. فالدور الجديد مابيرثش أي صلاحية.</para>
/// </summary>
public enum UserRole
{
    Technician = 0,
    Manager = 1,
    Owner = 2,
    FloorManager = 3,
    Accountant = 4
}

public enum RackStatus
{
    PendingPairing = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3
}

/// <summary>قوة التعرّف على الجهاز — <c>A</c> أقوى من <c>C</c>.</summary>
public enum DeviceIdentityConfidence
{
    None = 0,
    A = 1,
    B = 2,
    C = 3
}

public enum DeviceIdentifierKind
{
    SystemUuid = 0,
    BiosSerial = 1,
    BoardSerial = 2,
    DiskSerial = 3,
    MacAddress = 4,
    PanelEdidSerial = 5,
    BatterySerial = 6,
    CompanyCode = 7
}

public enum DeviceLifecycleStatus
{
    Active = 0,
    DuplicateSuspected = 1,
    Merged = 2,
    Retired = 3
}

public enum DeviceCodeLeaseStatus
{
    Open = 0,
    Exhausted = 1,
    Cancelled = 2
}

public enum TechnicianSpecialty
{
    None = 0,
    Testing = 1,
    Boards = 2,
    Batteries = 3,
    Screens = 4,
    Refinishing = 5,
    Grading = 6
}

/// <summary>مرحلة الجهاز في الورشة.</summary>
public enum DeviceOperationalStage
{
    Unknown = 0,
    Received = 1,
    Testing = 2,
    Tested = 3,
    NeedsRepair = 4,
    UnderRepair = 5,
    Ready = 6,
    WithSales = 7,
    AtPointOfSale = 8,
    Returned = 9
}

public enum LocationKind
{
    Warehouse = 0,
    Testing = 1,
    Repair = 2,
    Sales = 3,
    PointOfSale = 4
}

/// <summary>
/// حالة أمر الصيانة.
///
/// <para>🔴 <b>الراكة بتقراها كأرقام، ومفيش ترتيب بينها.</b>
/// <c>Cancelled = 5</c> رقمها أكبر من <c>InProgress = 2</c>، فأي
/// مقارنة بالأكبر/الأصغر بتحسب الإلغاء «شغل اتعمل». الحواجز كلها
/// عضوية صريحة.</para>
/// </summary>
public enum RepairStatus
{
    New = 0,
    WaitingForRepair = 1,
    InProgress = 2,
    Completed = 3,
    UnableToRepair = 4,
    Cancelled = 5
}

/// <summary>
/// قرار المحاسب — <b>محور مستقل عن <see cref="RepairStatus"/></b>.
///
/// <para>⚠️ أمر «مستني موافقة» ممكن يكون <c>New</c> أو
/// <c>WaitingForRepair</c>: الحالة بتقول اللاب فين، والموافقة بتقول
/// هل مسموح يتحرك.</para>
/// </summary>
public enum RepairApproval
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum DeviceWorkflowEventType
{
    StageChanged = 0,
    CustodyHandoff = 1,
    LocationMoved = 2,
    SentToRepair = 3,
    RepairStarted = 4,
    RepairCompleted = 5,
    DispatchedToSales = 6,
    DispatchedToPointOfSale = 7,
    ReturnReceived = 8,
    CodeChanged = 9
}
