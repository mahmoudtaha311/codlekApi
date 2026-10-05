using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using Codlek.Core.Entities;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Core.Maintenance;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Maintenance.SeedFirstRun;

/// <inheritdoc cref="SeedFirstRunCommand"/>
public sealed class SeedFirstRunCommandHandler(
    IMaintenanceRepository maintenance,
    UserManager<ApplicationUser> identity,
    ILogger<SeedFirstRunCommandHandler> log)
    : IRequestHandler<SeedFirstRunCommand, Result<FirstRunSeedResult>>
{
    public async Task<Result<FirstRunSeedResult>> Handle(
        SeedFirstRunCommand command, CancellationToken cancellationToken)
    {
        if (await maintenance.AnyTenantAsync(cancellationToken))
            return Result.Success(new FirstRunSeedResult(false, null));

        var tenant = new Tenant { Name = FirstRunDefaults.TenantName };

        /*
          🔴 **الشركة والحساب في حفظة واحدة.**

          الشركة بتتضاف هنا من غير حفظ، و`CreateAsync` بيحفظ الاتنين مع
          بعض (نفس `DbContext`). لو اتحفظوا على مرتين ووقع الحساب، تفضل
          شركة من غير مالك — والتشغيل الجاي بيشوف «فيه شركة» ومابيعملش
          حاجة، فالقاعدة تفضل من غير ولا دخول للأبد.
        */
        maintenance.AddTenant(tenant);

        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = FirstRunDefaults.OwnerUsername,
            DisplayName = FirstRunDefaults.OwnerDisplayName,
            Role = UserRole.Owner,
            Code = AccountCode.New(),
            IsActive = true,

            // 🔴 الباسورد معروف للكل — بوابة التغيير الإجباري مابتسيبوش
            //    يعمل حاجة قبل ما يغيّره.
            MustChangePassword = true,

            LegacySalt = "",
        };

        /*
          ⚠️ **البصمة بإيدنا هنا — عكس إنشاء الحسابات من اللوحة، وده
          مقصود.**

          `CreateAsync(user, password)` بيشغّل قاعدة الطول (٨)،
          و`admin` خمس حروف — فكانت هترفض الحساب الوحيد اللي القاعدة
          الفاضية محتاجاه. والقديم بيعمل `admin / admin` بالظبط. الطول
          بيتطبّق وقت التغيير الإجباري، ومفيش فحص طول وقت الدخول.
        */
        owner.PasswordHash = identity.PasswordHasher.HashPassword(owner, FirstRunDefaults.OwnerPassword);

        var created = await identity.CreateAsync(owner);

        if (!created.Succeeded)
        {
            string reason = IdentityErrorText.Of(created.Errors, identity.Options.Password);
            return Result.Failure<FirstRunSeedResult>(MaintenanceErrors.OwnerSeedFailed(reason));
        }

        log.LogInformation("اتعملت شركة افتراضية: {Name}", tenant.Name);

        log.LogWarning(
            "اتعمل حساب مدير افتراضي ({User}/{Pass}) — لازم يتغيّر أول دخول",
            FirstRunDefaults.OwnerUsername, FirstRunDefaults.OwnerPassword);

        return Result.Success(new FirstRunSeedResult(true, tenant.Id));
    }
}
