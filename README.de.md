<div align="center">

# Unity Best Practices Analyzer

**Unity-C#-Performanceprobleme erkennen, bevor der Play Mode startet.**

Roslyn-Diagnosen · konservative Schnellkorrekturen · Burst, Jobs, DOTS/ECS · Unity 2021.3+

</div>

<div align="center">

[English](README.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [فارسی](README.fa.md) · [Русский](README.ru.md)

</div>

Ein ergänzender Roslyn-Analyzer mit 74 rauscharmen Diagnosen und 72 optionalen Schnellkorrekturen für Unity und leistungsorientiertes C#, die `Microsoft.Unity.Analyzers` noch nicht abdeckt. Alle Diagnosen haben standardmäßig den Schweregrad `Info`: Rider, Visual Studio und VS Code zeigen hilfreiche Aktionen an, ohne Build-Warnungen, Fehler oder Meldungen in der Unity Console zu erzeugen.

> **Direktlinks:** [Installation](README.md#installation) · [Regelübersicht](docs/rules/index.md) · [Konfiguration](docs/configuration.md) · [Sicherheit](docs/safety.md) · [Dokumentationsübersicht](docs/README.md)

## Warum dieser Analyzer?

- Optimiert häufige Unity-C#-Hotpaths für Speicherzuweisungen, `Camera.main`, `NativeArray`, `Mathf`, `List<T>`, LINQ und `StringBuilder`.
- Modernisiert Entities-1.x-Code mit überprüfungspflichtigen Aktionen für `Entities.ForEach`, `SystemAPI.Query` und `IJobEntity`.
- Kennzeichnet die Sicherheit jeder Korrektur: sichere Änderungen können automatisch angewendet werden, andere müssen geprüft werden.
- Kann als Unity-UPM-Paket, NuGet-Analyzer oder manuell importierte Roslyn-Analyzer-DLL installiert werden.

## Sicherheitsmodell

Jede Regel ist in der [generierten Regelübersicht](docs/rules/index.md) als `Safe`, `ReviewRequired` oder `Experimental` eingestuft. `ReviewRequired`- und `Experimental`-Regeln bieten kein globales **Fix All** an. Die Begründungen stehen in den [Sicherheitsentscheidungen](docs/safety.md).

`UBP0001` wird nur korrigiert, wenn eine lösungsweite Referenzanalyse nachweist, dass keine externen Zugriffe durch die Kapselung ungültig werden. DOTS-Migrationen (`UBP0058`–`UBP0070`) müssen einzeln geprüft werden, weil sich Ausführungszeitpunkt, Synchronisierung, Abhängigkeiten, Threadsicherheit und ECS-Planung ändern können.

## Schnellkorrekturen

| Bereich | Beispiele |
|---|---|
| Unity-Performance | `Camera.main` zwischenspeichern, `NativeArray`-Initialisierung vermeiden, `Shader.PropertyToID` zwischenspeichern |
| C#-Performance | Listenkapazität vorab zuweisen, LINQ vereinfachen, `Array.Empty<T>()` verwenden |
| Jobs und Burst | `BurstCompile` und `ReadOnly` ergänzen, verworfene `JobHandle` zuweisen |
| DOTS/ECS | `Entities.ForEach` nach `SystemAPI.Query` oder `IJobEntity` migrieren |
| API-Design | serialisierte Felder sicher kapseln und Namespaces vereinheitlichen |
| Refactorings | Parameter und Anweisungen verschieben, Methoden inline erweitern, Symbole entfernen |

Die vollständige Liste von `UBP0001` bis `UBP0077`, einschließlich Voraussetzungen, Beispiele und Sicherheitsklasse, steht in der [Regelübersicht](docs/rules/index.md). Die Schnellkorrekturtitel folgen der UI-Sprache der IDE, auch auf Deutsch.

## Installation

Empfohlen wird das UPM-Archiv `.tgz` aus der neuesten GitHub-Version:

1. In Unity **Window > Package Manager** öffnen.
2. **+ > Add package from tarball** wählen und das `.tgz` auswählen.
3. C#-Projektdateien neu erzeugen und die IDE neu starten.

Alternativ stehen ein geprüftes NuGet-Paket und die Analyzer-DLL bereit. Bei manueller Installation müssen in Unity **Auto Reference**, **Validate References** und alle Plattformen deaktiviert sowie das Asset-Label `RoslynAnalyzer` gesetzt werden. Ausführliche Hinweise und Fehlerbehebung befinden sich im [englischen Installationsleitfaden](README.md#installation).

## Konfiguration

Schweregrade und konservative Grenzwerte lassen sich über `.editorconfig` konfigurieren. Die [Konfigurationsanleitung](docs/configuration.md) beschreibt alle Optionen; [`config/ubp-safe.editorconfig`](config/ubp-safe.editorconfig), `ubp-performance`, `ubp-dots-migration` und `ubp-all` sind direkt nutzbare Vorgaben.

## Build und Tests

```powershell
dotnet run --project tests/UnityBestPractices.Analyzers.Tests
dotnet test tests/UnityBestPractices.Analyzers.Tests.Xunit
dotnet pack src/UnityBestPractices.Analyzers -c Release -o artifacts
```

Der Analyzer zielt bewusst auf `netstandard2.0` und Roslyn 3.8, damit er mit Unitys .NET-Standard-2.1-Spielerprofilen kompatibel bleibt. CI prüft Builds, Tests, NuGet-/UPM-Pakete, Dokumentationskonsistenz und Performance.

## Verwendung in Unity

Nach der Installation lädt Unity den Analyzer beim Kompilieren; kompatible IDEs finden die Code-Fix-Provider aus derselben Assembly. Setze den Cursor auf eine gepunktet unterstrichene Empfehlung und öffne die Schnellaktionen (in Rider `Alt+Enter`). Alle Deskriptoren verwenden `DiagnosticSeverity.Info`, sodass sie als unaufdringliche Empfehlungen und nicht als Compilerwarnungen erscheinen.
