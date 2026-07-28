using System.Globalization;

namespace UnityBestPractices.Analyzers;

/// <summary>Provides quick-fix labels in the IDE's current UI language.</summary>
internal static class FixTitleLocalizer
{
#pragma warning disable RS1035 // Code fixes are displayed by the host UI and must follow its UI culture.
    internal static string Get(string diagnosticId, string englishTitle) =>
        Get(
            diagnosticId,
            englishTitle,
            CultureInfo.CurrentUICulture,
            CultureInfo.CurrentCulture,
            CultureInfo.InstalledUICulture);
#pragma warning restore RS1035

    internal static string Get(
        string diagnosticId,
        string englishTitle,
        CultureInfo currentUICulture,
        CultureInfo currentCulture,
        CultureInfo installedUICulture) =>
        Get(
            diagnosticId,
            englishTitle,
            ResolveCulture(currentUICulture, currentCulture, installedUICulture));

    internal static string Get(string diagnosticId, string englishTitle, CultureInfo culture)
    {
        var language = culture.TwoLetterISOLanguageName;
        if (language == "ja")
        {
            return TranslateJapanese(diagnosticId, englishTitle);
        }

        if (language == "fa")
        {
            return TranslatePersian(diagnosticId, englishTitle);
        }

        if (language == "ru")
        {
            return TranslateRussian(diagnosticId, englishTitle);
        }

        return englishTitle;
    }

    private static CultureInfo ResolveCulture(
        CultureInfo currentUICulture,
        CultureInfo currentCulture,
        CultureInfo installedUICulture)
    {
        // Some IDE analyzer hosts force their worker process UI culture to
        // English. Prefer a real localized UI culture, but when it is English
        // consult the user's process/OS cultures before falling back.
        if (IsLocalized(currentUICulture))
        {
            return currentUICulture;
        }

        if (IsLocalized(currentCulture))
        {
            return currentCulture;
        }

        return IsLocalized(installedUICulture)
            ? installedUICulture
            : currentUICulture;
    }

    private static bool IsLocalized(CultureInfo culture)
    {
        var language = culture.TwoLetterISOLanguageName;
        return language == "ja" || language == "fa" || language == "ru";
    }

    private static string TranslateJapanese(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        DiagnosticIds.EncapsulateSerializedField => "private にして SerializeField を追加",
        DiagnosticIds.YieldNull => "null を yield する",
        DiagnosticIds.AddBurstCompile => "BurstCompile を追加",
        DiagnosticIds.MarkNativeArrayReadOnly => "NativeArray を ReadOnly としてマーク",
        DiagnosticIds.UseRefLocal => "ref ローカル経由で変更",
        DiagnosticIds.CacheCameraMain => "このブロックで Camera.main をキャッシュ",
        DiagnosticIds.PreallocateList => "List の容量を事前確保",
        DiagnosticIds.UseMultiplicationForSquare => "値を自身と乗算",
        DiagnosticIds.EntitiesForEachToSystemApiQuery => "SystemAPI.Query の foreach に変換",
        DiagnosticIds.EntitiesForEachToJobEntityRun or DiagnosticIds.SystemApiQueryToJobEntityRun => "IJobEntity.Run に変換",
        DiagnosticIds.EntitiesForEachToJobEntitySchedule or DiagnosticIds.SystemApiQueryToJobEntitySchedule => "IJobEntity.Schedule に変換",
        DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel or DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel => "IJobEntity.ScheduleParallel に変換",
        DiagnosticIds.JobEntityRunToSchedule or DiagnosticIds.JobEntityScheduleParallelToSchedule => "Schedule 実行を使用",
        DiagnosticIds.JobEntityRunToScheduleParallel or DiagnosticIds.JobEntityScheduleToScheduleParallel => "ScheduleParallel 実行を使用",
        DiagnosticIds.JobEntityScheduleToRun or DiagnosticIds.JobEntityScheduleParallelToRun => "Run 実行を使用",
        DiagnosticIds.DiscardedScheduledJobHandle => "スケジュール済み JobHandle を代入",
        DiagnosticIds.CacheShaderPropertyId => "Shader.PropertyToID の結果をキャッシュ",
        _ => TranslateUse(englishTitle, "次を使用: "),
    };

    private static string TranslatePersian(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        DiagnosticIds.EncapsulateSerializedField => "private کردن فیلد و افزودن SerializeField",
        DiagnosticIds.YieldNull => "yield کردن null",
        DiagnosticIds.AddBurstCompile => "افزودن BurstCompile",
        DiagnosticIds.MarkNativeArrayReadOnly => "علامت‌گذاری NativeArray به‌عنوان ReadOnly",
        DiagnosticIds.UseRefLocal => "تغییر از طریق متغیر محلی ref",
        DiagnosticIds.CacheCameraMain => "کش کردن Camera.main در این بلوک",
        DiagnosticIds.PreallocateList => "پیش‌اختصاص ظرفیت List",
        DiagnosticIds.UseMultiplicationForSquare => "ضرب مقدار در خودش",
        DiagnosticIds.EntitiesForEachToSystemApiQuery => "تبدیل به foreach ‏SystemAPI.Query",
        DiagnosticIds.EntitiesForEachToJobEntityRun or DiagnosticIds.SystemApiQueryToJobEntityRun => "تبدیل به IJobEntity.Run",
        DiagnosticIds.EntitiesForEachToJobEntitySchedule or DiagnosticIds.SystemApiQueryToJobEntitySchedule => "تبدیل به IJobEntity.Schedule",
        DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel or DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel => "تبدیل به IJobEntity.ScheduleParallel",
        DiagnosticIds.JobEntityRunToSchedule or DiagnosticIds.JobEntityScheduleParallelToSchedule => "استفاده از اجرای Schedule",
        DiagnosticIds.JobEntityRunToScheduleParallel or DiagnosticIds.JobEntityScheduleToScheduleParallel => "استفاده از اجرای ScheduleParallel",
        DiagnosticIds.JobEntityScheduleToRun or DiagnosticIds.JobEntityScheduleParallelToRun => "استفاده از اجرای Run",
        DiagnosticIds.DiscardedScheduledJobHandle => "انتساب JobHandle زمان‌بندی‌شده",
        DiagnosticIds.CacheShaderPropertyId => "کش کردن نتیجهٔ Shader.PropertyToID",
        _ => TranslateUse(englishTitle, "استفاده از "),
    };

    private static string TranslateRussian(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        DiagnosticIds.EncapsulateSerializedField => "Сделать поле private и добавить SerializeField",
        DiagnosticIds.YieldNull => "Вернуть null через yield",
        DiagnosticIds.AddBurstCompile => "Добавить BurstCompile",
        DiagnosticIds.MarkNativeArrayReadOnly => "Пометить NativeArray как ReadOnly",
        DiagnosticIds.UseRefLocal => "Изменять через локальную переменную ref",
        DiagnosticIds.CacheCameraMain => "Кэшировать Camera.main в этом блоке",
        DiagnosticIds.PreallocateList => "Выделить ёмкость List заранее",
        DiagnosticIds.UseMultiplicationForSquare => "Умножить значение на само себя",
        DiagnosticIds.EntitiesForEachToSystemApiQuery => "Преобразовать в foreach с SystemAPI.Query",
        DiagnosticIds.EntitiesForEachToJobEntityRun or DiagnosticIds.SystemApiQueryToJobEntityRun => "Преобразовать в IJobEntity.Run",
        DiagnosticIds.EntitiesForEachToJobEntitySchedule or DiagnosticIds.SystemApiQueryToJobEntitySchedule => "Преобразовать в IJobEntity.Schedule",
        DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel or DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel => "Преобразовать в IJobEntity.ScheduleParallel",
        DiagnosticIds.JobEntityRunToSchedule or DiagnosticIds.JobEntityScheduleParallelToSchedule => "Использовать выполнение Schedule",
        DiagnosticIds.JobEntityRunToScheduleParallel or DiagnosticIds.JobEntityScheduleToScheduleParallel => "Использовать выполнение ScheduleParallel",
        DiagnosticIds.JobEntityScheduleToRun or DiagnosticIds.JobEntityScheduleParallelToRun => "Использовать выполнение Run",
        DiagnosticIds.DiscardedScheduledJobHandle => "Присвоить запланированный JobHandle",
        DiagnosticIds.CacheShaderPropertyId => "Кэшировать результат Shader.PropertyToID",
        _ => TranslateUse(englishTitle, "Использовать "),
    };

    private static string TranslateUse(string englishTitle, string prefix) =>
        englishTitle.StartsWith("Use ", System.StringComparison.Ordinal)
            ? prefix + englishTitle.Substring("Use ".Length)
            : englishTitle;
}
