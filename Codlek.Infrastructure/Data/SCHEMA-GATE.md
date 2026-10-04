# بوابة الـschema — إزاي نتأكد إن الكيانات مطابقة لقاعدة الإنتاج

## ليه البوابة دي موجودة

المشروع القديم هو اللي **بيملك** الـ٣١ جدول الموجودين. والجديد
بيتوصّل على نفس الـschema.

يعني: أي اختلاف بين الكيانات هنا والجداول الحقيقية = EF بيفتكر الـschema
غلط. والنتيجة مش رسالة خطأ — هي إن أول هجرة تتعمل بعدين هتحاول تعدّل
جدول فيه شغل ورشة حقيقي.

## القاعدة بالظبط

| | |
|---|---|
| **الهجرات بتتعمل فين** | `codlek_dev` — قاعدة محلية فاضية، مش الإنتاج |
| **زيادة جدول جديد** | ✅ مسموح |
| **تعديل أي حاجة في الـ٣١ جدول** | 🚫 ممنوع |

🔴 **والممنوع ده بيتقاس، مش بيتوعد بيه.** كل هجرة جديدة لازم تعدّي
القسم الأول في المقارنة تحت وهو **فاضي**. لو فيه سطر واحد، يبقى
الهجرة بتلمس شغل موجود.

### الجداول اللي اتزادت لحد دلوقتي

| الجدول | الهجرة | ليه |
|---|---|---|
| `RefreshTokens` | `AddRefreshTokens` | التوكن لازم ينفع يتلغي — صف بدل ورقة |
| `AspNetUsers` · `AspNetRoles` · `AspNetUserRoles` · `AspNetUserClaims` · `AspNetRoleClaims` · `AspNetUserLogins` · `AspNetUserTokens` | `AddIdentityTables` | مستخدمي اللوحة والأدوار |

⚠️ وجدول `Users` القديم **فضل زي ما هو**. الاتنين بيعيشوا مع بعض
فترة التحويل: القديم بيكتب في `Users`، والجديد في `AspNetUsers`،
ونقل الحسابات بيحصل مرة واحدة وقت التحويل.

### 🔴 فخ اتمسك فعلاً وقت إضافة Identity

`IdentityDbContext` فيها خاصية اسمها `Users` — ونفس الاسم كان على
`DbSet<WebUser>`. فاضطرينا نسمّي القديمة `WebUsers`.

و**اسم الجدول في EF بييجي من اسم الخاصية لو مش مكتوب**. يعني مجرد
تغيير الاسم كان كفاية. جرّبناها بالعمد — شلنا السطر وطلّعنا هجرة:

```
DropForeignKey  FK_Users_Departments_DepartmentId
DropForeignKey  FK_Users_Tenants_TenantId
DropPrimaryKey  PK_Users
RenameTable     Users → WebUsers
RenameIndex     IX_Users_TenantId_Username → ...
```

على جدول فيه **كل حسابات الورشة**. والسطر اللي بيمنع ده:

```csharp
b.Entity<WebUser>().ToTable("Users");
```

⚠️ **الدرس**: أي كيان اسم الـDbSet بتاعه اتغيّر، لازم `ToTable` صريح
في نفس اللحظة.

🔴 **طول نص واحد غلط بواحد كفاية.** جرّبناها: غيّرنا `MaxLength(60)`
لـ`(61)` على عمود واحد، وEF طلّع `AlterColumn` على طول.

## إزاي تشغّلها

**١ · خط الأساس — من القاعدة الحقيقية**

```bash
SCR=".../scratchpad"
sqlcmd -S localhost -E -d codlek_test -I -h-1 -W -i "$(cygpath -w "$SCR/dump_schema.sql")" \
  | grep -E "^[A-Za-z]" | sed 's/ *$//' | sort > real_schema.txt

sqlcmd -S localhost -E -d codlek_test -I -h-1 -W -i "$(cygpath -w "$SCR/dump_indexes.sql")" \
  | grep -E "^[A-Za-z]" | sed 's/ *$//' | sort > real_indexes.txt
```

⚠️ `codlek_test` هي اللي المشروع القديم بيهاجرها، فهي صورة مطابقة
للإنتاج. **متستعملش الإنتاج نفسها.**

**٢ · الشكل اللي EF فاهمه — في `codlek_dev`**

دي الخطوة العادية بعد أي هجرة جديدة:

```bash
dotnet build Codlek.Infrastructure/Codlek.Infrastructure.csproj -m:1
dotnet ef migrations add <اسم_الهجرة> --project Codlek.Infrastructure --no-build
dotnet build Codlek.Infrastructure/Codlek.Infrastructure.csproj -m:1
dotnet ef database update --project Codlek.Infrastructure --no-build
```

🔴 **بص على الهجرة بعينك قبل ما تطبّقها:**

```bash
sed -n '/void Up/,/^        }$/p' \
  Codlek.Infrastructure/Migrations/*_<اسم_الهجرة>.cs \
  | grep -oE "migrationBuilder\.[A-Za-z]+" | sort | uniq -c
```

المسموح: `CreateTable` و`CreateIndex` وبس. أي `AlterColumn` أو
`RenameTable` أو `DropColumn` معناه إن الهجرة بتلمس شغل موجود —
**وقّف**.

⚠️ `DesignTimeDbContextFactory` بيوجّه كل أوامر `ef` على `codlek_dev`
افتراضياً، فمفيش أمر ينفع يروح على الإنتاج بالغلط.

**٢ب · لو عايز تقيس من الصفر (نادر)**

لو محتاج تتأكد إن الـ٣١ كيان نفسهم لسه مطابقين من غير أي زيادة، اعمل
قاعدة فاضية تانية ووجّه `DesignTimeDbContextFactory` عليها **في فرع
منفصل**، وشيل مجلد `Migrations` واعمل هجرة واحدة:

```bash
sqlcmd -S localhost -E -I -Q "DROP DATABASE IF EXISTS codlek_shape; \
  CREATE DATABASE codlek_shape COLLATE Arabic_CI_AI"
```

🔴 **متعملش ده على الفرع الشغّال.** `rm -f Migrations/*.cs` بيمسح
الهجرات الحقيقية — واللي بعدها `codlek_dev` بيبقى فيه جداول مالهاش
هجرات، فأي `database update` بيحاول يعملهم من جديد ويرمي.

**٣ · المقارنة**

⚠️ **المقارنة باتجاهين، مش `diff` واحد.** لأن الزيادة مسموحة بقى،
فـ`diff` عادي بيطلّع سطور للجديد وبيبقى صعب تقرا منه الإجابة.

```bash
# ١ · قديم اتغيّر أو اتشال — لازم يبقى فاضي 🔴
comm -23 real_schema.txt  dev_schema.txt
comm -23 real_indexes.txt dev_indexes.txt

# ٢ · الجديد بس — للمراجعة
comm -13 real_schema.txt dev_schema.txt | awk -F'|' '{print $1}' | sort | uniq -c
```

🔴 **واطبع عدد السطور قبل المقارنة.** حصل هنا إن `sqlcmd` فشل
والملفين طلعوا فاضيين، فالمقارنة عدّت وهي مش بتقيس حاجة:

```bash
wc -l < real_schema.txt   # لازم ٤٠٣
wc -l < dev_schema.txt    # لازم ٤٦٢
```

⚠️ **وسبب الفشل ده بالتحديد:** `sqlcmd` على ويندوز بيقرا `/` كبداية
خيار. فمسار بشكل يونكس زي `/c/Users/...` بيتقرا `/U` = اسم مستخدم،
وبيطلّع «‎-E and -U are mutually exclusive». الحل:

```bash
sqlcmd ... -i "$(cygpath -w "$SCR/dump_schema.sql")"
```

## النتيجة وقت ما اتعملت (٤ أكتوبر ٢٠٢٦)

**أول قياس — قبل أي زيادة:**

| | الحقيقي | الجديد | |
|---|---|---|---|
| جداول | ٣١ | ٣١ | ✔ |
| أعمدة (اسم · نوع · طول · nullable) | ٤٠٣ | ٤٠٣ | **مطابق** |
| فهارس (أعمدة · فريد · فلتر) | ٦٨ | ٦٨ | **مطابق** |

**بعد `AddRefreshTokens` و`AddIdentityTables`:**

| | الحقيقي | `codlek_dev` | |
|---|---|---|---|
| جداول | ٣١ | ٣٩ | +٨ جداول جديدة |
| أعمدة | ٤٠٣ | ٤٦٢ | +٥٩ كلها في الجديد |
| فهارس | ٦٨ | ٧٩ | +١١ كلها في الجديد |
| **قديم اتغيّر** | — | — | **صفر** ✔ |

🔴 والسطر الأخير هو المهم: **ولا عمود ولا فهرس في الـ٣١ جدول اتحرك.**

## ⚠️ فخ لازم تعرفه

`dotnet ef migrations remove` بيشيل **آخر هجرة**، مش اللي إنت فاكر.
حصل هنا: شلت هجرة تجريبية فشال خط الأساس معاها. لو حصلت، امسح مجلد
`Migrations` كله وأعد من الخطوة ٢ — القاعدة معزولة فمفيش خسارة.

وفخ تاني: الأمر بيقول أحياناً «pending changes» بعد أول هجرة مباشرة.
اتأكد بإنك تعمل هجرة تجريبية تانية وتشوف `Up()` بتاعتها — لو فاضية،
الموديل مطابق والتحذير من تتابع الأوامر مش من فرق حقيقي.
