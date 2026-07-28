<div align="center">

# Unity Best Practices Analyzer

**مشکلات کارایی Unity C# را پیش از ورود به Play Mode پیدا کنید.**

تشخیص Roslyn · اصلاح‌های سریع محافظه‌کارانه · Burst، Jobs و DOTS/ECS · Unity 2021.3+

</div>

<div align="center">

[English](README.md) · [日本語](README.ja.md) · [فارسی](README.fa.md) · [Русский](README.ru.md)

</div>

یک تحلیل‌گر تکمیلی Roslyn با ۷۴ تشخیص کم‌مزاحمت و ۷۲ اصلاح سریع اختیاری برای بهترین روش‌های Unity و C# پربازده که هنوز توسط `Microsoft.Unity.Analyzers` پوشش داده نشده‌اند. شدت پیش‌فرض همهٔ تشخیص‌ها `Info` است تا Rider و Visual Studio اکشن‌ها را نمایش دهند، بدون آن‌که Build خطا یا هشدار تولید کند یا Unity Console شلوغ شود.

> **پیوندهای سریع:** [نصب](README.md#installation) · [فهرست قواعد](docs/rules/index.md) · [پیکربندی](docs/configuration.md) · [ایمنی](docs/safety.md) · [راهنمای مستندات](docs/README.md)

## اصلاح‌های سریع

| شناسه | مورد شناسایی‌شده | اصلاح |
|---|---|---|
| `UBP0001` | فیلد public قابل‌سریال‌سازی در `MonoBehaviour` یا `ScriptableObject` | `[UnityEngine.SerializeField]` را اضافه و فیلد را private می‌کند |
| `UBP0002` | عبارت `yield return 0` یا Boolean جعبه‌سازی‌شونده در Coroutine یونیتی | مقدار را با `null` جایگزین می‌کند تا بدون Boxing یک فریم منتظر بماند |
| `UBP0003` | عبارت `vector.magnitude < 10f` و مقایسه‌های معادل با یک ثابت مثبت | از `vector.sqrMagnitude < (10f * 10f)` استفاده می‌کند |
| `UBP0004` | ساختار `IJob*` یونیتی که Burst برایش فعال نیست | `[Unity.Burst.BurstCompile]` را اضافه می‌کند |
| `UBP0005` | فیلد `NativeArray<T>` در Job که فقط‌خواندنی بودن آن قابل اثبات است | `[Unity.Collections.ReadOnly]` را اضافه می‌کند |
| `UBP0006` | آرایهٔ Managed موقت و کوچک که به `Span<T>` یا `ReadOnlySpan<T>` صریح نسبت داده شده است | تخصیص آرایه را با `stackalloc` جایگزین می‌کند |
| `UBP0007` | عنصر Struct که کپی و تغییر داده شده و سپس در همان خانهٔ مجموعه بازنویسی می‌شود | از متغیر محلی `ref` استفاده و کپی‌کردن به عقب را حذف می‌کند |
| `UBP0008` | دو یا چند دسترسی به `Camera.main` در یک بلوک | دوربین را در یک متغیر محلی بدون تداخل نام Cache می‌کند |
| `UBP0009` | یک `List<T>` بدون ظرفیت اولیه و حداقل پنج فراخوانی متوالی `Add` | لیست را با حداقل ظرفیت معلوم مقداردهی می‌کند |
| `UBP0010` | عبارت `Mathf.Pow(value, 2f)` که `value` یک پارامتر یا متغیر محلی float است | فراخوانی عمومی توان را با `value * value` جایگزین می‌کند |
| `UBP0011` | یک `NativeArray<T>` با پاک‌سازی پیش‌فرض که بلافاصله در یک حلقهٔ استاندارد تمام‌بازه کاملاً بازنویسی می‌شود | `NativeArrayOptions.UninitializedMemory` را اضافه می‌کند |

### اصلاح‌های سریع بیشتر

قواعد توسعه‌یافته بر اساس نوع Syntax سازمان‌دهی شده‌اند. برای افزودن یک بهینه‌سازی عبارت جدید، تنها تعریف قاعده و Matcher لازم است؛ ثبت تشخیص، شدت پیشنهادی `Info`، ثبت اصلاح سریع، قالب‌بندی و پشتیبانی Fix All مشترک هستند.

| شناسه | مورد شناسایی‌شده | اصلاح |
|---|---|---|
| `UBP0012` | `new Vector2(0f, 0f)` | از `UnityEngine.Vector2.zero` استفاده می‌کند |
| `UBP0013` | `new Vector2(1f, 1f)` | از `UnityEngine.Vector2.one` استفاده می‌کند |
| `UBP0014` | `new Vector2(0f, 1f)` | از `UnityEngine.Vector2.up` استفاده می‌کند |
| `UBP0015` | `new Vector2(0f, -1f)` | از `UnityEngine.Vector2.down` استفاده می‌کند |
| `UBP0016` | `new Vector2(-1f, 0f)` | از `UnityEngine.Vector2.left` استفاده می‌کند |
| `UBP0017` | `new Vector2(1f, 0f)` | از `UnityEngine.Vector2.right` استفاده می‌کند |
| `UBP0018` | `new Vector3(0f, 0f, 0f)` | از `UnityEngine.Vector3.zero` استفاده می‌کند |
| `UBP0019` | `new Vector3(1f, 1f, 1f)` | از `UnityEngine.Vector3.one` استفاده می‌کند |
| `UBP0020` | `new Vector3(0f, 1f, 0f)` | از `UnityEngine.Vector3.up` استفاده می‌کند |
| `UBP0021` | `new Vector3(0f, -1f, 0f)` | از `UnityEngine.Vector3.down` استفاده می‌کند |
| `UBP0022` | `new Vector3(-1f, 0f, 0f)` | از `UnityEngine.Vector3.left` استفاده می‌کند |
| `UBP0023` | `new Vector3(1f, 0f, 0f)` | از `UnityEngine.Vector3.right` استفاده می‌کند |
| `UBP0024` | `new Vector3(0f, 0f, 1f)` | از `UnityEngine.Vector3.forward` استفاده می‌کند |
| `UBP0025` | `new Vector3(0f, 0f, -1f)` | از `UnityEngine.Vector3.back` استفاده می‌کند |
| `UBP0026` | `new Quaternion(0f, 0f, 0f, 1f)` | از `UnityEngine.Quaternion.identity` استفاده می‌کند |
| `UBP0027` | `Quaternion.Euler(0f, 0f, 0f)` | از `UnityEngine.Quaternion.identity` استفاده می‌کند |
| `UBP0028` | `new Color(0f, 0f, 0f, 0f)` | از `UnityEngine.Color.clear` استفاده می‌کند |
| `UBP0029` | `new Color(0f, 0f, 0f, 1f)` | از `UnityEngine.Color.black` استفاده می‌کند |
| `UBP0030` | `new Color(1f, 1f, 1f, 1f)` | از `UnityEngine.Color.white` استفاده می‌کند |
| `UBP0031` | `new Color(1f, 0f, 0f, 1f)` | از `UnityEngine.Color.red` استفاده می‌کند |
| `UBP0032` | `new Color(0f, 1f, 0f, 1f)` | از `UnityEngine.Color.green` استفاده می‌کند |
| `UBP0033` | `new Color(0f, 0f, 1f, 1f)` | از `UnityEngine.Color.blue` استفاده می‌کند |
| `UBP0034` | ساخت مقدار استاندارد زرد RGBA در Unity | از `UnityEngine.Color.yellow` استفاده می‌کند |
| `UBP0035` | `new Color(0f, 1f, 1f, 1f)` | از `UnityEngine.Color.cyan` استفاده می‌کند |
| `UBP0036` | `new Color(1f, 0f, 1f, 1f)` | از `UnityEngine.Color.magenta` استفاده می‌کند |
| `UBP0037` | `Mathf.Clamp(value, 0f, 1f)` | از `UnityEngine.Mathf.Clamp01(value)` استفاده می‌کند |
| `UBP0038` | `Mathf.Pow(value, 0.5f)` | از `UnityEngine.Mathf.Sqrt(value)` استفاده می‌کند |
| `UBP0039` | `(int)Mathf.Floor(value)` | از `UnityEngine.Mathf.FloorToInt(value)` استفاده می‌کند |
| `UBP0040` | `(int)Mathf.Ceil(value)` | از `UnityEngine.Mathf.CeilToInt(value)` استفاده می‌کند |
| `UBP0041` | `(int)Mathf.Round(value)` | از `UnityEngine.Mathf.RoundToInt(value)` استفاده می‌کند |
| `UBP0042` | `new T[0]` یا مقداردهی اولیهٔ آرایهٔ خالی با نوع صریح | از نمونهٔ Cacheشدهٔ `System.Array.Empty<T>()` استفاده می‌کند |
| `UBP0043` | `source.Where(predicate).Any()` با Predicate بدون Index | از `source.Any(predicate)` استفاده می‌کند |
| `UBP0044` | `source.Where(predicate).Count()` با Predicate بدون Index | از `source.Count(predicate)` استفاده می‌کند |
| `UBP0045` | `source.Where(predicate).First()` با Predicate بدون Index | از `source.First(predicate)` استفاده می‌کند |
| `UBP0046` | `source.Where(predicate).FirstOrDefault()` با Predicate بدون Index | از `source.FirstOrDefault(predicate)` استفاده می‌کند |
| `UBP0047` | `dictionary.Keys.Contains(key)` | از `dictionary.ContainsKey(key)` استفاده می‌کند |
| `UBP0048` | `list.ElementAt(index)` روی یک `List<T>` مشخص | از `list[index]` استفاده می‌کند |
| `UBP0049` | `list.Count()` روی یک `List<T>` مشخص | از ویژگی `list.Count` استفاده می‌کند |
| `UBP0050` | `array.Count()` روی آرایهٔ یک‌بُعدی | از ویژگی `array.Length` استفاده می‌کند |
| `UBP0051` | `array.Any()` روی آرایهٔ یک‌بُعدی | از `array.Length != 0` استفاده می‌کند |
| `UBP0052` | `list.Any()` روی یک `List<T>` مشخص | از `list.Count != 0` استفاده می‌کند |
| `UBP0053` | `StringBuilder.Append("x")` با ثابت یک‌نویسه‌ای | از Overload نویسه‌ای `Append('x')` استفاده می‌کند |
| `UBP0054` | `StringBuilder.AppendLine("")` | از `AppendLine()` بدون پارامتر استفاده می‌کند |
| `UBP0055` | `new CancellationToken()` | از `System.Threading.CancellationToken.None` استفاده می‌کند |
| `UBP0056` | `new Guid()` | از `System.Guid.Empty` استفاده می‌کند |
| `UBP0057` | `Enumerable.Empty<T>().ToArray()` | مستقیماً از `System.Array.Empty<T>()` Cacheشده استفاده می‌کند |

### اصلاح‌های سریع Query در DOTS

این اصلاح‌های سریع از سیستم‌های Query فعلی Entities 1.x استفاده می‌کنند. `SystemAPI.Query` مقصد `foreach` روی Thread اصلی است؛ `IJobEntity.Run`، `Schedule` و `ScheduleParallel` به‌ترتیب اجرای فوری، Job زمان‌بندی‌شدهٔ تکی و Job زمان‌بندی‌شدهٔ موازی را فراهم می‌کنند. Entities 1.x رابط Query جداگانه‌ای به نام `IJobParallel` ندارد و اجرای موازی IJobEntity با `ScheduleParallel` انجام می‌شود.

| شناسه | مورد شناسایی‌شده | اصلاح |
|---|---|---|
| `UBP0058` | یک `Entities.ForEach(...).Run()` سازگار روی Thread اصلی | آن را به `foreach` روی `SystemAPI.Query<RefRW<...>, RefRO<...>>()` تبدیل می‌کند |
| `UBP0059` | یک Pipeline سازگار `Entities.ForEach` | یک `IJobEntity` با Burst استخراج و `Run()` را فراخوانی می‌کند |
| `UBP0060` | یک Pipeline سازگار `Entities.ForEach` | یک `IJobEntity` با Burst استخراج و `Schedule()` را فراخوانی می‌کند |
| `UBP0061` | یک Pipeline سازگار `Entities.ForEach` | یک `IJobEntity` با Burst استخراج و `ScheduleParallel()` را فراخوانی می‌کند |
| `UBP0062` | حلقهٔ foreach سازگار `SystemAPI.Query` | یک `IJobEntity` با Burst استخراج و `Run()` را فراخوانی می‌کند |
| `UBP0063` | حلقهٔ foreach سازگار `SystemAPI.Query` | یک `IJobEntity` با Burst استخراج و `Schedule()` را فراخوانی می‌کند |
| `UBP0064` | حلقهٔ foreach سازگار `SystemAPI.Query` | یک `IJobEntity` با Burst استخراج و `ScheduleParallel()` را فراخوانی می‌کند |
| `UBP0065` | فراخوانی بدون پارامتر `IJobEntity.Run()` | اجرا را به `Schedule()` تغییر می‌دهد |
| `UBP0066` | فراخوانی بدون پارامتر `IJobEntity.Run()` | اجرا را به `ScheduleParallel()` تغییر می‌دهد |
| `UBP0067` | فراخوانی بدون پارامتر `IJobEntity.Schedule()` | اجرا را به `Run()` تغییر می‌دهد |
| `UBP0068` | فراخوانی بدون پارامتر `IJobEntity.Schedule()` | اجرا را به `ScheduleParallel()` تغییر می‌دهد |
| `UBP0069` | فراخوانی بدون پارامتر `IJobEntity.ScheduleParallel()` | اجرا را به `Run()` تغییر می‌دهد |
| `UBP0070` | فراخوانی بدون پارامتر `IJobEntity.ScheduleParallel()` | اجرا را به `Schedule()` تغییر می‌دهد |

### صحت و Cache

| شناسه | مورد شناسایی‌شده | اصلاح |
|---|---|---|
| `UBP0071` | دور انداختن `Unity.Jobs.JobHandle` بازگشتی از یک `Schedule` پشتیبانی‌شده | Handle را در متغیر محلی بدون تداخل نام ذخیره می‌کند تا قابل انتقال یا ترکیب باشد |
| `UBP0072` | `NativeArray<T>` محلی و استفاده‌نشده با `Allocator.Persistent` که نشت آن به‌صورت محدود قابل اثبات است | فقط تشخیص؛ مالکیت و محل Dispose به تصمیم توسعه‌دهنده نیاز دارد |
| `UBP0073` | خروج `NativeArray<T>` با `Temp` یا `TempJob` از طریق return، فیلد یا Delegate طولانی‌عمر | فقط تشخیص؛ طول عمر صحیح وابسته به برنامه است |
| `UBP0074` | فراخوانی تکراری `Shader.PropertyToID` با ثابت یکسان در یک Type | فیلد `static readonly` با نام یکتا می‌سازد و فراخوانی‌ها را جایگزین می‌کند |
| `UBP0075` | فایل Type بدون Namespace که فایل‌های هم‌پوشه‌اش یک Namespace غالب و بدون ابهام دارند | Typeهای فایل را در Namespace فایل‌های همسایه قرار می‌دهد |

تحلیل‌گر، Symbolهای Unity را به‌شکل معنایی Resolve می‌کند. نوع‌های نامرتبط با نام اعضای مشابه، نوع فیلدهای پشتیبانی‌نشده، Iteratorهای غیر Unity، آستانه‌های فاصلهٔ پویا، کد تولیدشده و نسخه‌های Unity یا Package فاقد Symbolهای لازم نادیده گرفته می‌شوند.

تبدیل‌های کارایی محدودیت‌های ایمنی محافظه‌کارانه دارند. تخصیص روی Stack فقط برای عناصر Primitive یا Enum، خارج از حلقه و با اندازهٔ کل حداکثر ۱ KiB پیشنهاد می‌شود؛ در صورت نیاز برای حفظ مقداردهی صفر آرایهٔ Managed، اصلاح `Span.Clear()` را درج می‌کند. تبدیل به متغیر محلی `ref` به مسیر دسترسی واقعی با بازگشت ref، Receiver و Index بدون تغییر، یک Mutation شناسایی‌شده و عدم استفاده از کپی محلی پس از بازنویسی متناظر نیاز دارد. فیلد Job فقط زمانی به‌صورت فقط‌خواندنی پیشنهاد می‌شود که همهٔ استفاده‌های آن در Job از نوع خواندن شناخته‌شده باشند. ظرفیت List تنها از دستورهای پیوستهٔ `Add` استنتاج می‌شود. تبدیل به ضرب برای توان دوم به شناسه‌های Scalar بدون عارضهٔ جانبی محدود است. حافظهٔ Native مقداردهی‌نشده فقط زمانی پیشنهاد می‌شود که دستور بعدی یک حلقهٔ استاندارد باشد که بدون خواندن محتوای قبلی آرایه، همهٔ Indexها را مقداردهی کند.

استخراج DOTS فقط برای فراخوانی معنایی Unity Entities با پارامترهای مستقیم `IComponentData`، فیلترهای پشتیبانی‌شدهٔ `WithAll`، `WithAny`، `WithNone`، `WithChangeFilter` یا گزینه‌های Query و بدنه‌ای بدون Localهای Captureشده، Lambdaهای تو‌در‌تو یا دسترسی به Instance سیستم پیشنهاد می‌شود. پارامتر `ref` به `RefRW<T>`، پارامتر `in` یا مقداری به `RefRO<T>` و پارامتر Entity به `WithEntityAccess()` تبدیل می‌شود؛ فیلترهای Query نیز به Attribute معادل IJobEntity تبدیل می‌شوند. حلقه‌های موجود `SystemAPI.Query` باید از طریق `ValueRW` یا `ValueRO` به Wrapperها دسترسی داشته باشند تا هدف دسترسی هنگام استخراج حفظ شود.

## مواردی که عمداً خارج از محدوده‌اند

این Package با فهرست فعلی `Microsoft.Unity.Analyzers` از `UNT0001` تا `UNT0043` بررسی شده است. قواعد موجودی مانند پیام‌های خالی Unity، `CompareTag`، `TryGetComponent`، APIهای Physics بدون تخصیص، دستورهای yield کش‌شده، APIهای موقعیت و چرخش Transform، دسترسی حلقه‌ای به آرایه‌های Mesh یا `Animator.StringToHash` عمداً تکرار نشده‌اند.

قواعد از راهنمای رسمی [Jobهای کامپایل‌شده با Burst](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/compilation-burstcompile.html)، [NativeContainerهای فقط‌خواندنی](https://docs.unity3d.com/Manual/job-system-native-container.html)، [`SystemAPI.Query`](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.SystemAPI.Query.html)، [زمان‌بندی `IJobEntity`](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.IJobEntityExtensions.ScheduleParallel.html)، [Cache کردن `Camera.main`](https://docs.unity3d.com/ScriptReference/Camera-main.html)، [`NativeArrayOptions.UninitializedMemory`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArrayOptions.UninitializedMemory.html)، [`stackalloc` محدود](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/stackalloc) و [جلوگیری از کپی با ref در C#](https://learn.microsoft.com/dotnet/csharp/advanced-topics/performance/) پیروی می‌کنند.

## ایمنی، نصب و پیکربندی

هر قاعده یکی از طبقه‌بندی‌های `Safe`، `ReviewRequired` یا `Experimental` را دارد. قواعد `ReviewRequired` و `Experimental` از Fix All استفاده نمی‌کنند. اصلاح `UBP0001` فقط وقتی نمایش داده می‌شود که تحلیل Reference در کل Solution ثابت کند ارجاع ناسازگار بیرونی وجود ندارد. تبدیل‌های `UBP0058` تا `UBP0070` ممکن است زمان اجرا، همگام‌سازی، Dependency، Thread Safety و زمان‌بندی ECS را تغییر دهند و باید تک‌به‌تک بررسی شوند. [فهرست قواعد](docs/rules/index.md) جزئیات را دارد.

روش پیشنهادی نصب، دریافت فایل UPM با پسوند `.tgz` از GitHub Release و انتخاب **Package Manager > Add package from tarball** است. Release همچنین `.nupkg`، `.snupkg`، DLL با نام استاندارد و Checksum دارد. برای نصب دستی DLL، گزینه‌های **Auto Reference**، **Validate References** و همهٔ Platformها را غیرفعال و Label دقیق `RoslynAnalyzer` را تنظیم کنید.

شدت‌ها و آستانه‌های محافظه‌کارانه از طریق `.editorconfig` قابل تنظیم‌اند. [راهنمای پیکربندی](docs/configuration.md) و Presetهای [`config`](config) را ببینید. تحلیل‌گر `netstandard2.0` و Roslyn 3.8 را حفظ می‌کند و با پروفایل Player مبتنی بر .NET Standard 2.1 یونیتی سازگار است.

## Build و Test

```powershell
dotnet run --project tests/UnityBestPractices.Analyzers.Tests
dotnet pack src/UnityBestPractices.Analyzers -c Release -o artifacts
```

تست‌ها هر ۷۴ Descriptor، قواعد دارای اصلاح، مستندات، سیاست Fix All، Referenceهای کل Solution، همهٔ تبدیل‌های DOTS، موارد منفی محافظه‌کارانه، محتوای Package، سازگاری و آستانه‌های کلی Performance را بررسی می‌کنند.

### Release خودکار

هر Push و Pull Request، Build، همهٔ تست‌ها، اعتبارسنجی NuGet/UPM، هماهنگی مستندات و بررسی Performance را اجرا می‌کند. Release فقط از Tag دقیق SemVer مطابق نسخهٔ پروژه، مانند `v0.4.0`، ساخته می‌شود. اجرای دوباره نسخهٔ دیگری نمی‌سازد و DLL، `.nupkg`، `.snupkg`، فایل UPM با پسوند `.tgz` و `SHA256SUMS` منتشر می‌شوند. انتشار در NuGet.org فقط با Secret به نام `NUGET_API_KEY` فعال است.

## استفاده در Unity

پروفایل اسکریپت‌نویسی Player در Unity از .NET Standard 2.1 پشتیبانی می‌کند. DLL تحلیل‌گر طبق الزام [راهنمای Roslyn Analyzer یونیتی](https://docs.unity3d.com/2023.2/Documentation/Manual/roslyn-analyzers.html) عمداً `netstandard2.0` را هدف می‌گیرد و در نتیجه با پروژه‌هایی که از پروفایل Player مبتنی بر .NET Standard 2.1 استفاده می‌کنند سازگار است. Solution شامل یک پروژهٔ سازگاری `netstandard2.1` است که به هر دو Entry Point عمومی تحلیل‌گر ارجاع می‌دهد؛ تمام Buildهای محلی و CI این پروژه را کامپایل می‌کنند تا از بازگشت ناسازگاری جلوگیری شود.

1. تحلیل‌گر را Build یا Pack کنید.
2. فایل `UnityBestPractices.Analyzers.dll` را از `bin/Release/netstandard2.0` یا دایرکتوری `analyzers/dotnet/cs` در Package نوگت، به پوشه‌ای زیر `Assets` پروژهٔ Unity کپی کنید.
3. DLL را در Plugin Inspector یونیتی انتخاب کنید. گزینه‌های **Auto Reference**، **Validate References**، **Any Platform**، **Editor** و **Standalone** را غیرفعال کنید و Label دقیق `RoslynAnalyzer` را به Asset اختصاص دهید.
4. تنظیمات Import را Apply کنید، پروژهٔ C# را دوباره تولید کنید و Visual Studio یا Rider را Restart کنید. در Rider همچنین فعال بودن **Settings | Editor | Inspection Settings | Roslyn Analyzers | Enable Roslyn analyzers** را بررسی کنید.
5. Caret را روی پیشنهاد با خط نقطه‌ای کم‌رنگ قرار دهید و Quick Action محیط توسعه را اجرا کنید (در Rider کلید `Alt+Enter`).

Unity تحلیل‌گر را برای کامپایل بارگذاری می‌کند و IDE پشتیبانی‌شده، Code Fix Provider را از همان Assembly پیدا می‌کند. `Microsoft.CodeAnalysis.Workspaces` و `System.Composition` وابستگی‌هایی هستند که میزبان IDE برای Code Fix Provider فراهم می‌کند؛ به همین دلیل اعتبارسنجی Referenceهای Asset در Unity باید غیرفعال بماند. چون همهٔ Descriptorها از `DiagnosticSeverity.Info` استفاده می‌کنند، به‌شکل پیشنهاد کم‌مزاحمت همراه Light Bulb نمایش داده می‌شوند، نه Warning یا Error کامپایلر.
