using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Users;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Auth;
using Codlek.Core.Entities.Auth;
using Codlek.Core.Enums;
using Codlek.Core.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Application.Features.Users.CreateUser;

/// <summary>
/// بيعمل حساب لوحة تحكم جديد.
///
/// <para>⚠️ ترتيب الفحوص هنا <b>نفس ترتيب المشروع القديم بالحرف</b> —
/// الشكل الأول، وبعده الصلاحية، وبعده التكرار. أي ترتيب تاني بيدّي
/// رسالة خطأ مختلفة لنفس الطلب.</para>
/// </summary>
public sealed class CreateUserCommandHandler(
    IUserAccountRepository users,
    UserManager<ApplicationUser> identity,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser me)
    : IRequestHandler<CreateUserCommand, Result<UserAccountResult>>
{
    public async Task<Result<UserAccountResult>> Handle(
        CreateUserCommand command, CancellationToken cancellationToken)
    {
        /*
          🔴 **الرقم بيتفحص قبل الكاست.**

          `(UserRole)99` كاست شرعي في C# — بيعدّي من غير أي شكوى.
          وساعتها `CanCreate` بيرجّع true للمالك (حالة `Owner => true`
          مابتبصش على القيمة)، والصف بيتكتب بدور مالوش اسم:
          `UserRoleText` بيقع على الافتراضي فالصفحة بتعرضه «فني»، وكل
          حاجز صلاحية بيرفضه لأنه مش بيساوي أي قيمة معروفة.

          **حساب ميّت بيتعمل بنجاح.**
        */
        if (!Enum.IsDefined(typeof(UserRole), command.Role))
            return Result.Failure<UserAccountResult>(UserErrors.UnknownRole);

        var wanted = (UserRole)command.Role;

        // ⚠️ الاسم بيتصغّر — مفتاح الدخول مش حسّاس للحالة.
        string username = command.Username.Trim().ToLowerInvariant();
        string displayName = command.DisplayName.Trim();

        if (username.Length < UserManagementRules.MinUsernameLength)
            return Result.Failure<UserAccountResult>(UserErrors.UsernameTooShort);

        /*
          🔴 **الحد الأعلى مش زيادة على الحد الأدنى.**

          الأعمدة مقيّدة، وEF مابيشغّلش تحقق DataAnnotations وقت
          الحفظ — بيبعت القيمة زي ما هي وSQL Server بيرمي «String or
          binary data would be truncated». الاستثناء ده مش متمسك،
          فبيوصل للواجهة **500** بدل رسالة، والإجراء مابيحصلش أصلاً.

          ⚠️ وفيه عدم تماثل كان بيخفي ده في القديم: التطبيع بيقص
          المفتاح على 60 عشان الفحص، لكن اللي بيتخزّن في العمود هو
          الاسم **الكامل**. يعني اسم 70 حرف كان بيعدّي كل الفحوص
          وبيقع عند الحفظ.
        */
        if (username.Length > UserManagementRules.MaxUsernameLength)
            return Result.Failure<UserAccountResult>(UserErrors.UsernameTooLong);

        if (displayName.Length < UserManagementRules.MinDisplayNameLength)
            return Result.Failure<UserAccountResult>(UserErrors.DisplayNameTooShort);

        if (displayName.Length > UserManagementRules.MaxDisplayNameLength)
            return Result.Failure<UserAccountResult>(UserErrors.DisplayNameTooLong);

        if (!UserManagementRules.CanCreate(me.Role, wanted))
            return Result.Failure<UserAccountResult>(UserErrors.CannotCreateThatRole);

        string key = LoginName.Normalize(username);

        if (await users.UsernameTakenAnywhereAsync(key, cancellationToken))
            return Result.Failure<UserAccountResult>(UserErrors.UsernameTaken);

        string? code = await NewCodeAsync(cancellationToken);

        if (code is null) return Result.Failure<UserAccountResult>(UserErrors.CodeExhausted);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = me.TenantId,
            UserName = username,
            DisplayName = displayName,
            Role = wanted,
            Code = code,
            IsActive = true,

            /*
              🔴 **الباسورد ده اللي بيعمل الحساب كتبه بإيده.**

              هو بيسلّمه على واتساب أو بالكلام، فهو — وأي حد شاف
              الرسالة — عارفه. من غير الإجبار بيفضل شغّال شهور
              والحساب عملياً مشترك بين صاحبه ومديره، وكل سطر في سجل
              التدقيق مكتوب باسمه يبقى قابل للإنكار.
            */
            MustChangePassword = true,

            // ⚠️ **الملح القديم فاضي عن قصد.** الحساب الجديد باسورده
            // بالشكل الجديد من أول لحظة — راجع `LegacyPasswordHasher`.
            LegacySalt = "",
        };

        /*
          ⚠️ **الباسورد بيتحطّ بـ`UserManager`، مش ببصمة بالإيد.**

          لأنه هو اللي بينده `IPasswordHasher` المسجَّل وبيشغّل قواعد
          القوة (`IdentityOptions.Password`). ولو كتبنا البصمة
          بإيدينا، كل مسار بيعيّن باسورد كان لازم يكرّر القواعد — وأول
          واحد ينساها بيفتح باب خلفي.
        */
        var created = await identity.CreateAsync(user, command.Password);

        if (!created.Succeeded)
        {
            string reason = IdentityErrorText.Of(created.Errors, identity.Options.Password);
            return Result.Failure<UserAccountResult>(UserErrors.PasswordRejected(reason));
        }

        /*
          🔴 التسجيل على النجاح بس، وبعد كل الفحوص — سجل بيقول
          «اتعمل حساب» لحساب مااتعملش بيخلّي السجل كله مش موثوق.

          ⚠️ والباسورد الأولي عمره ما بيدخل السجل.
        */
        audit.Record(
            AuditActions.WebUserCreated, "User", user.Id, user.Code,
            $"اتعمل حساب «{displayName}» بصلاحية {UserRoleText.Arabic(wanted)}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserAccountResult(user.Id, user.DisplayName, user.Code));
    }

    /// <summary>
    /// كود حساب جديد، فريد <b>جوّه الشركة</b>.
    ///
    /// <para>⚠️ الكود ده هو اللي بيربط الحساب بالفحوصات، فتكراره
    /// بيخلط شغل ناس ببعضه. ٤٠ محاولة وبعدين فشل — أحسن من كود مكرر
    /// يعدّي في صمت.</para>
    /// </summary>
    private async Task<string?> NewCodeAsync(CancellationToken ct)
    {
        for (int i = 0; i < 40; i++)
        {
            string code = AccountCode.New();

            if (!await users.CodeTakenAsync(me.TenantId, code, ct)) return code;
        }

        return null;
    }
}
