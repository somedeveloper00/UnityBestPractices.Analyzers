using UnityBestPractices.Analyzers;
using System.Globalization;

namespace UnityBestPractices.Analyzers.Infrastructure;

/// <summary>Provides quick-fix labels in the IDE's current UI language.</summary>
internal static class FixTitleLocalizer
{
    internal const string ConvertStringLiteralToNameof = "refactoring.convert-string-to-nameof";
    internal const string ConvertLocalToField = "refactoring.convert-local-to-field";
    internal const string ConvertSystemBaseToISystem = "refactoring.convert-system-base-to-isystem";
    internal const string InlineMethod = "refactoring.inline-method";
    internal const string MoveParameterLeft = "refactoring.move-parameter-left";
    internal const string MoveParameterRight = "refactoring.move-parameter-right";
    internal const string MoveStatementUp = "refactoring.move-statement-up";
    internal const string MoveStatementDown = "refactoring.move-statement-down";
    internal const string MoveStatementLeft = "refactoring.move-statement-left";
    internal const string MoveStatementRight = "refactoring.move-statement-right";
    internal const string RemoveParameter = "refactoring.remove-parameter";
    internal const string RemoveDoubleEmptyLines = "refactoring.remove-double-empty-lines";
    internal const string RemoveEmptyBrackets = "refactoring.remove-empty-brackets";
    internal const string RemoveSymbol = "refactoring.remove-symbol";

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

        if (language == "de")
        {
            return TranslateGerman(diagnosticId, englishTitle);
        }

        if (language == "pl")
        {
            return TranslatePolish(diagnosticId, englishTitle);
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
        return language == "ja" || language == "fa" || language == "ru" || language == "de" || language == "pl";
    }

    private static string TranslateJapanese(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        ConvertStringLiteralToNameof => "文字列リテラルを nameof に置換",
        ConvertLocalToField => "ローカル変数をフィールドに変換",
        ConvertSystemBaseToISystem => "SystemBase を ISystem に変換",
        InlineMethod => "メソッドをインライン化",
        MoveParameterLeft => "パラメーターを左へ移動",
        MoveParameterRight => "パラメーターを右へ移動",
        MoveStatementUp => "ステートメントを上へ移動",
        MoveStatementDown => "ステートメントを下へ移動",
        MoveStatementLeft => "ステートメントを左へ移動",
        MoveStatementRight => "ステートメントを右へ移動",
        RemoveParameter => "パラメーターを削除",
        RemoveDoubleEmptyLines => "連続する空行を削除",
        RemoveEmptyBrackets => "空の括弧を削除",
        RemoveSymbol => "シンボルとすべての使用箇所を削除",
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
        DiagnosticIds.RemoveUnusedEntityAccess => "未使用のエンティティアクセスを削除",
        _ => TranslateUse(englishTitle, "次を使用: "),
    };

    private static string TranslatePersian(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        ConvertStringLiteralToNameof => "جایگزینی لفظ رشته با nameof",
        ConvertLocalToField => "تبدیل متغیر محلی به فیلد",
        ConvertSystemBaseToISystem => "تبدیل SystemBase به ISystem",
        InlineMethod => "درون‌خطی کردن متد",
        MoveParameterLeft => "انتقال پارامتر به چپ",
        MoveParameterRight => "انتقال پارامتر به راست",
        MoveStatementUp => "انتقال دستور به بالا",
        MoveStatementDown => "انتقال دستور به پایین",
        MoveStatementLeft => "انتقال دستور به چپ",
        MoveStatementRight => "انتقال دستور به راست",
        RemoveParameter => "حذف پارامتر",
        RemoveDoubleEmptyLines => "حذف خطوط خالی تکراری",
        RemoveEmptyBrackets => "حذف پرانتزهای خالی",
        RemoveSymbol => "حذف نماد و همهٔ کاربردهای آن",
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
        DiagnosticIds.RemoveUnusedEntityAccess => "حذف دسترسی استفاده‌نشده به موجودیت",
        _ => TranslateUse(englishTitle, "استفاده از "),
    };

    private static string TranslateRussian(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        ConvertStringLiteralToNameof => "Заменить строковый литерал на nameof",
        ConvertLocalToField => "Преобразовать локальную переменную в поле",
        ConvertSystemBaseToISystem => "Преобразовать SystemBase в ISystem",
        InlineMethod => "Встроить метод",
        MoveParameterLeft => "Переместить параметр влево",
        MoveParameterRight => "Переместить параметр вправо",
        MoveStatementUp => "Переместить инструкцию вверх",
        MoveStatementDown => "Переместить инструкцию вниз",
        MoveStatementLeft => "Переместить инструкцию влево",
        MoveStatementRight => "Переместить инструкцию вправо",
        RemoveParameter => "Удалить параметр",
        RemoveDoubleEmptyLines => "Удалить повторяющиеся пустые строки",
        RemoveEmptyBrackets => "Удалить пустые скобки",
        RemoveSymbol => "Удалить символ и все его использования",
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
        DiagnosticIds.RemoveUnusedEntityAccess => "Удалить неиспользуемый доступ к сущности",
        _ => TranslateUse(englishTitle, "Использовать "),
    };

    private static string TranslateGerman(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        ConvertStringLiteralToNameof => "Zeichenfolgenliteral durch nameof ersetzen",
        ConvertLocalToField => "Lokale Variable in ein Feld konvertieren",
        ConvertSystemBaseToISystem => "SystemBase in ISystem konvertieren",
        InlineMethod => "Methode inline erweitern",
        MoveParameterLeft => "Parameter nach links verschieben",
        MoveParameterRight => "Parameter nach rechts verschieben",
        MoveStatementUp => "Anweisung nach oben verschieben",
        MoveStatementDown => "Anweisung nach unten verschieben",
        MoveStatementLeft => "Anweisung nach links verschieben",
        MoveStatementRight => "Anweisung nach rechts verschieben",
        RemoveParameter => "Parameter entfernen",
        RemoveDoubleEmptyLines => "Doppelte Leerzeilen entfernen",
        RemoveEmptyBrackets => "Leere Klammern entfernen",
        RemoveSymbol => "Symbol und alle Verwendungen entfernen",
        DiagnosticIds.EncapsulateSerializedField => "Feld als private festlegen und SerializeField hinzufügen",
        DiagnosticIds.YieldNull => "null mit yield zurückgeben",
        DiagnosticIds.AddBurstCompile => "BurstCompile hinzufügen",
        DiagnosticIds.MarkNativeArrayReadOnly => "NativeArray als ReadOnly markieren",
        DiagnosticIds.UseRefLocal => "Über eine lokale ref-Variable ändern",
        DiagnosticIds.CacheCameraMain => "Camera.main in diesem Block zwischenspeichern",
        DiagnosticIds.PreallocateList => "List-Kapazität vorab zuweisen",
        DiagnosticIds.UseMultiplicationForSquare => "Wert mit sich selbst multiplizieren",
        DiagnosticIds.EntitiesForEachToSystemApiQuery => "In eine SystemAPI.Query-foreach-Schleife konvertieren",
        DiagnosticIds.EntitiesForEachToJobEntityRun or DiagnosticIds.SystemApiQueryToJobEntityRun => "In IJobEntity.Run konvertieren",
        DiagnosticIds.EntitiesForEachToJobEntitySchedule or DiagnosticIds.SystemApiQueryToJobEntitySchedule => "In IJobEntity.Schedule konvertieren",
        DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel or DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel => "In IJobEntity.ScheduleParallel konvertieren",
        DiagnosticIds.JobEntityRunToSchedule or DiagnosticIds.JobEntityScheduleParallelToSchedule => "Schedule-Ausführung verwenden",
        DiagnosticIds.JobEntityRunToScheduleParallel or DiagnosticIds.JobEntityScheduleToScheduleParallel => "ScheduleParallel-Ausführung verwenden",
        DiagnosticIds.JobEntityScheduleToRun or DiagnosticIds.JobEntityScheduleParallelToRun => "Run-Ausführung verwenden",
        DiagnosticIds.DiscardedScheduledJobHandle => "Geplantes JobHandle zuweisen",
        DiagnosticIds.CacheShaderPropertyId => "Ergebnis von Shader.PropertyToID zwischenspeichern",
        DiagnosticIds.RemoveUnusedEntityAccess => "Nicht verwendeten Entitätszugriff entfernen",
        _ => TranslateUse(englishTitle, "Verwenden: "),
    };

    private static string TranslatePolish(string diagnosticId, string englishTitle) => diagnosticId switch
    {
        ConvertStringLiteralToNameof => "Zastąp literał ciągu operatorem nameof",
        ConvertLocalToField => "Przekonwertuj zmienną lokalną na pole",
        ConvertSystemBaseToISystem => "Przekonwertuj SystemBase na ISystem",
        InlineMethod => "Rozwiń metodę w miejscu wywołania",
        MoveParameterLeft => "Przenieś parametr w lewo",
        MoveParameterRight => "Przenieś parametr w prawo",
        MoveStatementUp => "Przenieś instrukcję w górę",
        MoveStatementDown => "Przenieś instrukcję w dół",
        MoveStatementLeft => "Przenieś instrukcję w lewo",
        MoveStatementRight => "Przenieś instrukcję w prawo",
        RemoveParameter => "Usuń parametr",
        RemoveDoubleEmptyLines => "Usuń powtarzające się puste wiersze",
        RemoveEmptyBrackets => "Usuń puste nawiasy",
        RemoveSymbol => "Usuń symbol i wszystkie jego użycia",
        DiagnosticIds.EncapsulateSerializedField => "Ustaw pole jako private i dodaj SerializeField",
        DiagnosticIds.YieldNull => "Zwróć null za pomocą yield",
        DiagnosticIds.AddBurstCompile => "Dodaj BurstCompile",
        DiagnosticIds.MarkNativeArrayReadOnly => "Oznacz NativeArray jako ReadOnly",
        DiagnosticIds.UseRefLocal => "Modyfikuj przez lokalną zmienną ref",
        DiagnosticIds.CacheCameraMain => "Buforuj Camera.main w tym bloku",
        DiagnosticIds.PreallocateList => "Wstępnie przydziel pojemność List",
        DiagnosticIds.UseMultiplicationForSquare => "Pomnóż wartość przez nią samą",
        DiagnosticIds.EntitiesForEachToSystemApiQuery => "Przekonwertuj na pętlę foreach z SystemAPI.Query",
        DiagnosticIds.EntitiesForEachToJobEntityRun or DiagnosticIds.SystemApiQueryToJobEntityRun => "Przekonwertuj na IJobEntity.Run",
        DiagnosticIds.EntitiesForEachToJobEntitySchedule or DiagnosticIds.SystemApiQueryToJobEntitySchedule => "Przekonwertuj na IJobEntity.Schedule",
        DiagnosticIds.EntitiesForEachToJobEntityScheduleParallel or DiagnosticIds.SystemApiQueryToJobEntityScheduleParallel => "Przekonwertuj na IJobEntity.ScheduleParallel",
        DiagnosticIds.JobEntityRunToSchedule or DiagnosticIds.JobEntityScheduleParallelToSchedule => "Użyj wykonywania Schedule",
        DiagnosticIds.JobEntityRunToScheduleParallel or DiagnosticIds.JobEntityScheduleToScheduleParallel => "Użyj wykonywania ScheduleParallel",
        DiagnosticIds.JobEntityScheduleToRun or DiagnosticIds.JobEntityScheduleParallelToRun => "Użyj wykonywania Run",
        DiagnosticIds.DiscardedScheduledJobHandle => "Przypisz zaplanowany JobHandle",
        DiagnosticIds.CacheShaderPropertyId => "Buforuj wynik Shader.PropertyToID",
        DiagnosticIds.RemoveUnusedEntityAccess => "Usuń nieużywany dostęp do encji",
        _ => TranslateUse(englishTitle, "Użyj: "),
    };

    private static string TranslateUse(string englishTitle, string prefix) =>
        englishTitle.StartsWith("Use ", System.StringComparison.Ordinal)
            ? prefix + englishTitle.Substring("Use ".Length)
            : englishTitle;
}
