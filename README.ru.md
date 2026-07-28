# Unity Best Practices Analyzer для Unity, Rider и Visual Studio

[English](README.md) | [日本語](README.ja.md) | [فارسی](README.fa.md) | [Русский](README.ru.md)

**Находите проблемы производительности Unity C# ещё до запуска игры.** Unity Best Practices Analyzer — это пакет Roslyn-анализаторов и исправлений кода для Unity 2021.3+, JetBrains Rider и Visual Studio. Он содержит 74 малошумных диагностики и 72 необязательных быстрых исправления для Unity, Burst, Jobs, DOTS/ECS и высокопроизводительного C#, которые не покрывает `Microsoft.Unity.Analyzers`.

Все диагностики по умолчанию имеют уровень `Info`: IDE показывает полезную подсказку без предупреждений и ошибок сборки и без лишних сообщений в Unity Console. Названия быстрых исправлений используют язык интерфейса IDE, включая русский, японский и персидский.

**Начните здесь:** [установить через Unity Package Manager](#unity-package-manager) · [настроить правила](#настройка) · [посмотреть быстрые исправления](#быстрые-исправления) · [проверить совместимость](#совместимость)

## Зачем нужен этот анализатор?

- Улучшает распространённые горячие пути Unity C# консервативными подсказками для выделений памяти, `Camera.main`, `NativeArray`, `Mathf`, `List<T>`, LINQ и `StringBuilder`.
- Помогает модернизировать код Entities 1.x: доступны проверяемые исправления для `Entities.ForEach`, `SystemAPI.Query` и `IJobEntity`.
- Явно указывает безопасность каждого исправления: одни можно применять автоматически, другие требуют просмотра кода.
- Устанавливается как пакет Unity UPM, анализатор NuGet или вручную импортированная DLL Roslyn-анализатора.

## Модель безопасности

Для каждого правила задана классификация в [сгенерированном индексе правил](docs/rules/index.md). Обоснования правил, требующих проверки, находятся в [решениях по безопасности правил](docs/safety.md).

| Классификация | Значение | Исправить всё |
| --- | --- | --- |
| `Safe` | Исправление должно сохранять наблюдаемое поведение при точно задокументированных условиях. | Доступно только когда изменение безопасно для выбранной области. |
| `ReviewRequired` | Могут измениться доступность, поведение чисел с плавающей точкой, время жизни памяти, потоки, синхронизация, сериализация или планирование ECS. | Никогда не предлагается глобальным исправлением `Fix All`. |
| `Experimental` | Правило включается по желанию, пока определяется область совместимости. | Не поддерживается. |

`UBP0001` требует проверки: исправление доступно только после анализа всей solution, подтверждающего отсутствие внешних ссылок, которые станут недоступны. Миграции DOTS (`UBP0058`–`UBP0070`) также требуют проверки, поскольку `Run`, `Schedule` и `ScheduleParallel` меняют момент выполнения, синхронизацию, зависимости, потокобезопасность и планирование.

## Категории правил

- `Unity.Performance.Safe` и `CSharp.Performance` содержат консервативные оптимизации выделений памяти и API.
- `Unity.Performance.Review` содержит преобразования производительности, требующие проверки в рантайме.
- `Unity.Correctness` охватывает зависимости задач и время жизни native-контейнеров.
- `Unity.DOTS.Migration` содержит миграции запросов и режимов выполнения Entities 1.x.
- `Unity.API.Design` содержит правила проектирования Unity API и сериализации.
- `CSharp.CodeStyle` содержит подсказки по стилю, определяемому по соседнему исходному коду.

## Быстрые исправления

Анализатор предлагает исправления для следующих распространённых случаев:

- сериализуемые public-поля `MonoBehaviour` и `ScriptableObject`;
- `yield return` со значениями, которые приводят к boxing;
- сравнение `magnitude`, `Mathf.Pow(value, 2f)` и другие избыточные вычисления;
- задания Unity без `BurstCompile`, поля `NativeArray<T>` только для чтения и лишняя очистка `NativeArray`;
- небольшие временные массивы, которые можно заменить на `stackalloc`, и ненужные копии структур вместо `ref`-локальных переменных;
- повторные обращения к `Camera.main`, повторяющиеся `Shader.PropertyToID` и последовательные `List<T>.Add`;
- канонические значения `Vector2`, `Vector3`, `Quaternion` и `Color`;
- `Mathf.Clamp`, `Mathf.Pow`, приведения `Floor`/`Ceil`/`Round`, пустые массивы, LINQ, `List<T>` и `StringBuilder`;
- `Entities.ForEach`, `SystemAPI.Query` и режимы выполнения `IJobEntity`;
- потерянные `JobHandle` и несоответствие пространства имён папке.

Полная таблица диагностик, распознаваемых форм и преобразований доступна в [английском README](README.md#quick-fixes) и [индексе правил](docs/rules/index.md). Вся документация правил использует стабильные идентификаторы `UBP0001`–`UBP0075`.

## Рефакторинги параметров и кода

Поставьте курсор на параметр метода, конструктора, локальной функции или индексатора и вызовите действие IDE, чтобы использовать **Move parameter left** или **Move parameter right**. Рефакторинг обновляет связанные интерфейсы и реализации, а также семантически сопоставленные вызовы C#, включая именованные и необязательные аргументы, инициализаторы конструкторов и сокращённые вызовы методов расширения.

Для простого статического метода доступно действие **Inline method**. Оно предлагается только тогда, когда каждый аргумент используется ровно один раз и в порядке вычисления, а границы параметров и результата не выполняют неявных преобразований.

Для строкового литерала, точно совпадающего с именем доступного символа, доступно **Replace string literal with nameof**. Действие проверяет, что замена остаётся той же строковой константой времени компиляции.

Действие **Remove parameter** удаляет параметр из связанных объявлений и соответствующие аргументы из семантически сопоставленных вызовов. Перемещение инструкций и объявлений доступно через **Move statement up** и **Move statement down**; комментарии и trivia перемещаются вместе с синтаксисом.

## Установка

### Unity Package Manager

Скачайте `com.somedeveloper.unity-best-practices-analyzers-<version>.tgz` из соответствующего [релиза GitHub](https://github.com/somedeveloper00/UnityBestPractices.Analyzers/releases), затем выберите **Window > Package Manager > + > Add package from tarball**. Пакет содержит DLL анализатора и файл `.meta` с меткой `RoslynAnalyzer` и отключённой проверкой ссылок.

Если версия Unity не сохраняет импортированные метки, выберите `Packages/Unity Best Practices Analyzers/Editor/Analyzers/UnityBestPractices.Analyzers.dll`, выключите **Auto Reference**, **Validate References** и все платформы, назначьте точную метку `RoslynAnalyzer` и примените настройки импорта.

### NuGet

Стабильные релизы содержат `UnityBestPractices.Analyzers.<version>.nupkg` и пакет символов. Пока публикация на NuGet.org не включена, скачайте пакет из GitHub Releases и добавьте его каталог как локальный источник пакетов:

```powershell
dotnet nuget add source C:\path\to\downloaded-packages -n UnityBestPracticesLocal
dotnet add package UnityBestPractices.Analyzers --version 0.4.20 --source UnityBestPracticesLocal
```

### Ручная установка DLL

1. Скачайте release-asset `UnityBestPractices.Analyzers.dll`.
2. Скопируйте его в каталог `Assets` Unity-проекта.
3. В Plugin Inspector выключите **Auto Reference**, **Validate References**, **Any Platform**, **Editor** и **Standalone**, затем добавьте точную метку `RoslynAnalyzer`.
4. Пересоздайте C#-проект и перезапустите Rider или Visual Studio. В Rider включите Roslyn-анализаторы в **Settings > Editor > Inspection Settings > Roslyn Analyzers**.
5. Поставьте курсор на подсказку и вызовите действие IDE (`Alt+Enter` в Rider).

## Настройка

Используйте стандартные настройки `dotnet_diagnostic.UBPxxxx.severity` и уровни серьёзности категорий, чтобы повысить уровень, подавить или отключить правила. Консервативные параметры задают предел `stackalloc`, порог предварительного выделения списка, миграции DOTS и правила, требующие проверки. См. [руководство по настройке](docs/configuration.md) и готовые пресеты в [`config`](config).

```ini
[*.cs]
dotnet_diagnostic.UBP0009.severity = warning
dotnet_diagnostic.UBP0058.severity = none
ubp_max_stackalloc_bytes = 512
ubp_enable_review_required = false
```

Отсутствующие или некорректные значения используют консервативные значения по умолчанию и не приводят к сбою анализа.

## Совместимость

Анализатор ориентирован на `netstandard2.0` и Roslyn 3.8 — консервативную базовую линию хоста анализаторов Unity. Он совместим с проектами, использующими профиль Unity .NET Standard 2.1, и не ссылается на сборки Unity во время выполнения: все Unity API и API пакетов разрешаются семантически.

| Среда | Проверяемая конфигурация |
| --- | --- |
| Базовая версия Unity | Unity 2021.3 LTS |
| Базовая версия DOTS | Unity 2022.3, Entities 1.0.11, Collections 2.1.4, Burst 1.8.2 |
| Текущая LTS-линейка | Фикстура манифеста Unity 6.3 LTS |

## Сборка и тесты

```powershell
dotnet restore UnityBestPractices.sln
dotnet build UnityBestPractices.sln -c Release --no-restore
dotnet run --project tests/UnityBestPractices.Analyzers.Tests -c Release --no-build
dotnet test tests/UnityBestPractices.Analyzers.Tests.Xunit -c Release --no-build --no-restore
```

Тесты проверяют все диагностические дескрипторы, исправляемые правила, документацию, политику `Fix All`, анализ ссылок всей solution, преобразования DOTS, консервативные отрицательные случаи, содержимое пакетов, совместимость и пороговые значения производительности.

## Лицензия

Распространяется по лицензии [MIT](LICENSE).
