<div align="center">

# Unity Best Practices Analyzer

**Wykrywaj problemy z wydajnością Unity C# jeszcze przed uruchomieniem Play Mode.**

Diagnostyka Roslyn · zachowawcze szybkie poprawki · Burst, Jobs, DOTS/ECS · Unity 2021.3+

</div>

<div align="center">

[English](README.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [فارسی](README.fa.md) · [Русский](README.ru.md)

</div>

Uzupełniający analizator Roslyn udostępniający 78 mało uciążliwych reguł diagnostycznych i 76 opcjonalnych szybkich poprawek dla Unity oraz wydajnego C#, których nie obejmuje jeszcze `Microsoft.Unity.Analyzers`. Domyślny poziom każdej diagnozy to `Info`, dzięki czemu Rider, Visual Studio i VS Code pokazują przydatne akcje bez ostrzeżeń kompilacji, błędów ani zbędnych komunikatów w Unity Console.

> **Szybkie odnośniki:** [instalacja](README.md#installation) · [indeks reguł](docs/rules/index.md) · [konfiguracja](docs/configuration.md) · [bezpieczeństwo](docs/safety.md) · [mapa dokumentacji](docs/README.md)

## Dlaczego warto używać analizatora?

- Usprawnia często wykonywany kod Unity C# związany z alokacjami, `Camera.main`, `NativeArray`, `Mathf`, `List<T>`, LINQ i `StringBuilder`.
- Modernizuje kod Entities 1.x za pomocą wymagających weryfikacji akcji dla `Entities.ForEach`, `SystemAPI.Query` i `IJobEntity`.
- Jawnie określa bezpieczeństwo każdej poprawki: bezpieczne zmiany można stosować automatycznie, pozostałe wymagają przeglądu.
- Może być instalowany jako pakiet Unity UPM, analizator NuGet albo ręcznie importowana biblioteka DLL analizatora Roslyn.

## Model bezpieczeństwa

Każda reguła jest sklasyfikowana w [generowanym indeksie reguł](docs/rules/index.md) jako `Safe`, `ReviewRequired` lub `Experimental`. Reguły `ReviewRequired` i `Experimental` nie udostępniają globalnej operacji **Fix All**. Uzasadnienia znajdują się w [decyzjach dotyczących bezpieczeństwa](docs/safety.md).

Poprawka `UBP0001` jest dostępna tylko wtedy, gdy analiza odwołań w całym rozwiązaniu wykaże, że hermetyzacja nie unieważni zewnętrznych odwołań. Migracje DOTS (`UBP0058`–`UBP0070`) należy sprawdzać pojedynczo, ponieważ mogą zmienić czas wykonania, synchronizację, zależności, bezpieczeństwo wątków i planowanie ECS.

## Szybkie poprawki

| Obszar | Przykłady |
|---|---|
| Wydajność Unity | buforowanie `Camera.main`, pomijanie inicjalizacji `NativeArray`, buforowanie `Shader.PropertyToID` |
| Wydajność C# | wstępne przydzielanie pojemności listy, upraszczanie LINQ, używanie `Array.Empty<T>()` |
| Jobs i Burst | dodawanie `BurstCompile` i `ReadOnly`, przypisywanie odrzuconych `JobHandle` |
| DOTS/ECS | migracja `Entities.ForEach` do `SystemAPI.Query` lub `IJobEntity` |
| Projekt API | bezpieczna hermetyzacja serializowanych pól i ujednolicanie przestrzeni nazw |
| Refaktoryzacje | przenoszenie parametrów i instrukcji, rozwijanie metod, usuwanie symboli |

Pełna lista reguł od `UBP0001` do `UBP0078`, wraz z warunkami, przykładami i klasą bezpieczeństwa, znajduje się w [indeksie reguł](docs/rules/index.md). Tytuły szybkich poprawek są dopasowane do języka interfejsu IDE, także po polsku.

## Instalacja

Zalecana metoda korzysta z archiwum UPM `.tgz` z najnowszego wydania GitHub:

1. W Unity otwórz **Window > Package Manager**.
2. Wybierz **+ > Add package from tarball** i wskaż plik `.tgz`.
3. Wygeneruj ponownie pliki projektu C# i uruchom ponownie IDE.

Dostępne są również zweryfikowany pakiet NuGet i biblioteka DLL analizatora. Przy instalacji ręcznej wyłącz w Unity opcje **Auto Reference**, **Validate References** i wszystkie platformy, a następnie ustaw etykietę zasobu `RoslynAnalyzer`. Szczegółowe instrukcje i rozwiązania problemów zawiera [angielski przewodnik instalacji](README.md#installation).

## Konfiguracja

Poziomy ważności i zachowawcze limity można ustawiać w `.editorconfig`. Wszystkie opcje opisuje [przewodnik konfiguracji](docs/configuration.md), a [`config/ubp-safe.editorconfig`](config/ubp-safe.editorconfig), `ubp-performance`, `ubp-dots-migration` i `ubp-all` to gotowe zestawy ustawień.

## Kompilowanie i testy

```powershell
dotnet run --project tests/UnityBestPractices.Analyzers.Tests
dotnet test tests/UnityBestPractices.Analyzers.Tests.Xunit
dotnet pack src/UnityBestPractices.Analyzers -c Release -o artifacts
```

Analizator celowo korzysta z `netstandard2.0` i Roslyn 3.8, aby zachować zgodność z profilami odtwarzacza Unity opartymi na .NET Standard 2.1. CI sprawdza kompilację, testy, pakiety NuGet/UPM, spójność dokumentacji i wydajność.

## Używanie w Unity

Po instalacji Unity ładuje analizator podczas kompilacji, a zgodne IDE odnajdują dostawców poprawek kodu w tym samym zestawie. Umieść kursor na sugestii oznaczonej delikatnym podkreśleniem i otwórz szybkie akcje (w Riderze `Alt+Enter`). Wszystkie deskryptory używają `DiagnosticSeverity.Info`, dlatego są wyświetlane jako dyskretne sugestie, a nie ostrzeżenia kompilatora.
