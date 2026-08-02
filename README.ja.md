<div align="center">

# Unity Best Practices Analyzer

**Play Mode に入る前に Unity C# のパフォーマンス問題を発見。**

Roslyn 診断 · 控えめなクイックフィックス · Burst、Jobs、DOTS/ECS · Unity 2021.3+

</div>

<div align="center">

[English](README.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [فارسی](README.fa.md) · [Русский](README.ru.md)

</div>

`Microsoft.Unity.Analyzers` ではまだ扱われていない Unity および高性能 C# のベストプラクティスを対象に、78 個の低ノイズ診断と 76 個の任意適用クイックフィックスを提供する Roslyn アナライザーです。すべての診断の既定重大度は `Info` です。Rider と Visual Studio はアクションを提示できますが、ビルドエラーや警告は発生せず、Unity Console に余計なメッセージも表示されません。

> **クイックリンク:** [インストール](README.md#installation) · [ルール一覧](docs/rules/index.md) · [設定](docs/configuration.md) · [安全性](docs/safety.md) · [ドキュメント案内](docs/README.md)

## クイックフィックス

| ID | 対象 | 修正内容 |
|---|---|---|
| `UBP0001` | `MonoBehaviour` または `ScriptableObject` のシリアライズ可能な public フィールド | `[UnityEngine.SerializeField]` を追加し、フィールドを private にします |
| `UBP0002` | Unity コルーチン内の `yield return 0` またはボックス化される Boolean | 値を `null` に置き換え、ボックス化せずに 1 フレーム待機します |
| `UBP0003` | `vector.magnitude < 10f` および正の定数との同等な比較 | `vector.sqrMagnitude < (10f * 10f)` を使用します |
| `UBP0004` | Burst が有効でない Unity の `IJob*` 構造体 | `[Unity.Burst.BurstCompile]` を追加します |
| `UBP0005` | ジョブ内で読み取り専用であることを証明できる `NativeArray<T>` フィールド | `[Unity.Collections.ReadOnly]` を追加します |
| `UBP0006` | 明示的な `Span<T>` または `ReadOnlySpan<T>` に代入される小さな一時マネージド配列 | 配列の割り当てを `stackalloc` に置き換えます |
| `UBP0007` | コピーして変更した後、同じコレクション位置へ書き戻される構造体要素 | `ref` ローカルを使用し、コピーの書き戻しを削除します |
| `UBP0008` | 同じブロック内にある 2 回以上の `Camera.main` アクセス | 名前衝突のないローカル変数へカメラをキャッシュします |
| `UBP0009` | 引数なしの `List<T>` の直後に 5 回以上連続する `Add` 呼び出し | 既知の最小容量を指定してリストを初期化します |
| `UBP0010` | float のローカル変数または引数に対する `Mathf.Pow(value, 2f)` | 汎用べき乗呼び出しを `value * value` に置き換えます |
| `UBP0011` | 標準の全範囲ループですぐに全要素が上書きされる、既定でクリアされる `NativeArray<T>` | `NativeArrayOptions.UninitializedMemory` を追加します |

### その他のクイックフィックス

拡張ルールは構文種別ごとに整理されています。式の最適化を追加する際はルール宣言とマッチャーだけを追加し、診断登録、`Info` 提案重大度、クイックフィックス登録、書式設定、Fix All 対応は共有されます。

| ID | 対象 | 修正内容 |
|---|---|---|
| `UBP0012` | `new Vector2(0f, 0f)` | `UnityEngine.Vector2.zero` を使用します |
| `UBP0013` | `new Vector2(1f, 1f)` | `UnityEngine.Vector2.one` を使用します |
| `UBP0014` | `new Vector2(0f, 1f)` | `UnityEngine.Vector2.up` を使用します |
| `UBP0015` | `new Vector2(0f, -1f)` | `UnityEngine.Vector2.down` を使用します |
| `UBP0016` | `new Vector2(-1f, 0f)` | `UnityEngine.Vector2.left` を使用します |
| `UBP0017` | `new Vector2(1f, 0f)` | `UnityEngine.Vector2.right` を使用します |
| `UBP0018` | `new Vector3(0f, 0f, 0f)` | `UnityEngine.Vector3.zero` を使用します |
| `UBP0019` | `new Vector3(1f, 1f, 1f)` | `UnityEngine.Vector3.one` を使用します |
| `UBP0020` | `new Vector3(0f, 1f, 0f)` | `UnityEngine.Vector3.up` を使用します |
| `UBP0021` | `new Vector3(0f, -1f, 0f)` | `UnityEngine.Vector3.down` を使用します |
| `UBP0022` | `new Vector3(-1f, 0f, 0f)` | `UnityEngine.Vector3.left` を使用します |
| `UBP0023` | `new Vector3(1f, 0f, 0f)` | `UnityEngine.Vector3.right` を使用します |
| `UBP0024` | `new Vector3(0f, 0f, 1f)` | `UnityEngine.Vector3.forward` を使用します |
| `UBP0025` | `new Vector3(0f, 0f, -1f)` | `UnityEngine.Vector3.back` を使用します |
| `UBP0026` | `new Quaternion(0f, 0f, 0f, 1f)` | `UnityEngine.Quaternion.identity` を使用します |
| `UBP0027` | `Quaternion.Euler(0f, 0f, 0f)` | `UnityEngine.Quaternion.identity` を使用します |
| `UBP0028` | `new Color(0f, 0f, 0f, 0f)` | `UnityEngine.Color.clear` を使用します |
| `UBP0029` | `new Color(0f, 0f, 0f, 1f)` | `UnityEngine.Color.black` を使用します |
| `UBP0030` | `new Color(1f, 1f, 1f, 1f)` | `UnityEngine.Color.white` を使用します |
| `UBP0031` | `new Color(1f, 0f, 0f, 1f)` | `UnityEngine.Color.red` を使用します |
| `UBP0032` | `new Color(0f, 1f, 0f, 1f)` | `UnityEngine.Color.green` を使用します |
| `UBP0033` | `new Color(0f, 0f, 1f, 1f)` | `UnityEngine.Color.blue` を使用します |
| `UBP0034` | Unity 標準の黄色 RGBA 値の生成 | `UnityEngine.Color.yellow` を使用します |
| `UBP0035` | `new Color(0f, 1f, 1f, 1f)` | `UnityEngine.Color.cyan` を使用します |
| `UBP0036` | `new Color(1f, 0f, 1f, 1f)` | `UnityEngine.Color.magenta` を使用します |
| `UBP0037` | `Mathf.Clamp(value, 0f, 1f)` | `UnityEngine.Mathf.Clamp01(value)` を使用します |
| `UBP0038` | `Mathf.Pow(value, 0.5f)` | `UnityEngine.Mathf.Sqrt(value)` を使用します |
| `UBP0039` | `(int)Mathf.Floor(value)` | `UnityEngine.Mathf.FloorToInt(value)` を使用します |
| `UBP0040` | `(int)Mathf.Ceil(value)` | `UnityEngine.Mathf.CeilToInt(value)` を使用します |
| `UBP0041` | `(int)Mathf.Round(value)` | `UnityEngine.Mathf.RoundToInt(value)` を使用します |
| `UBP0042` | `new T[0]` または型が明示された空の配列初期化子 | キャッシュ済みの `System.Array.Empty<T>()` インスタンスを使用します |
| `UBP0043` | インデックスを使わない述語を持つ `source.Where(predicate).Any()` | `source.Any(predicate)` を使用します |
| `UBP0044` | インデックスを使わない述語を持つ `source.Where(predicate).Count()` | `source.Count(predicate)` を使用します |
| `UBP0045` | インデックスを使わない述語を持つ `source.Where(predicate).First()` | `source.First(predicate)` を使用します |
| `UBP0046` | インデックスを使わない述語を持つ `source.Where(predicate).FirstOrDefault()` | `source.FirstOrDefault(predicate)` を使用します |
| `UBP0047` | `dictionary.Keys.Contains(key)` | `dictionary.ContainsKey(key)` を使用します |
| `UBP0048` | 具象 `List<T>` に対する `list.ElementAt(index)` | `list[index]` を使用します |
| `UBP0049` | 具象 `List<T>` に対する `list.Count()` | `list.Count` プロパティを使用します |
| `UBP0050` | 1 次元配列に対する `array.Count()` | `array.Length` プロパティを使用します |
| `UBP0051` | 1 次元配列に対する `array.Any()` | `array.Length != 0` を使用します |
| `UBP0052` | 具象 `List<T>` に対する `list.Any()` | `list.Count != 0` を使用します |
| `UBP0053` | 1 文字の定数を渡す `StringBuilder.Append("x")` | 文字オーバーロード `Append('x')` を使用します |
| `UBP0054` | `StringBuilder.AppendLine("")` | 引数なしの `AppendLine()` を使用します |
| `UBP0055` | `new CancellationToken()` | `System.Threading.CancellationToken.None` を使用します |
| `UBP0056` | `new Guid()` | `System.Guid.Empty` を使用します |
| `UBP0057` | `Enumerable.Empty<T>().ToArray()` | キャッシュ済みの `System.Array.Empty<T>()` を直接使用します |

### DOTS クエリのクイックフィックス

これらのクイックフィックスは、現在の Entities 1.x クエリシステムを使用します。`SystemAPI.Query` はメインスレッド上の `foreach` 変換先です。`IJobEntity.Run`、`Schedule`、`ScheduleParallel` はそれぞれ即時実行、単一スケジュールジョブ、並列スケジュールジョブを提供します。Entities 1.x には独立した `IJobParallel` クエリインターフェイスはなく、IJobEntity の並列実行には `ScheduleParallel` を使用します。

| ID | 対象 | 修正内容 |
|---|---|---|
| `UBP0058` | 対応可能なメインスレッドの `Entities.ForEach(...).Run()` | `SystemAPI.Query<RefRW<...>, RefRO<...>>()` を列挙する `foreach` に変換します |
| `UBP0059` | 対応可能な `Entities.ForEach` パイプライン | Burst 対応の `IJobEntity` を抽出して `Run()` を呼び出します |
| `UBP0060` | 対応可能な `Entities.ForEach` パイプライン | Burst 対応の `IJobEntity` を抽出して `Schedule()` を呼び出します |
| `UBP0061` | 対応可能な `Entities.ForEach` パイプライン | Burst 対応の `IJobEntity` を抽出して `ScheduleParallel()` を呼び出します |
| `UBP0062` | 対応可能な `SystemAPI.Query` foreach ループ | Burst 対応の `IJobEntity` を抽出して `Run()` を呼び出します |
| `UBP0063` | 対応可能な `SystemAPI.Query` foreach ループ | Burst 対応の `IJobEntity` を抽出して `Schedule()` を呼び出します |
| `UBP0064` | 対応可能な `SystemAPI.Query` foreach ループ | Burst 対応の `IJobEntity` を抽出して `ScheduleParallel()` を呼び出します |
| `UBP0065` | 引数なしの `IJobEntity.Run()` 呼び出し | 実行方法を `Schedule()` に切り替えます |
| `UBP0066` | 引数なしの `IJobEntity.Run()` 呼び出し | 実行方法を `ScheduleParallel()` に切り替えます |
| `UBP0067` | 引数なしの `IJobEntity.Schedule()` 呼び出し | 実行方法を `Run()` に切り替えます |
| `UBP0068` | 引数なしの `IJobEntity.Schedule()` 呼び出し | 実行方法を `ScheduleParallel()` に切り替えます |
| `UBP0069` | 引数なしの `IJobEntity.ScheduleParallel()` 呼び出し | 実行方法を `Run()` に切り替えます |
| `UBP0070` | 引数なしの `IJobEntity.ScheduleParallel()` 呼び出し | 実行方法を `Schedule()` に切り替えます |

### 正確性とキャッシュ

| ID | 対象 | 修正内容 |
|---|---|---|
| `UBP0071` | 対応する `Schedule` 呼び出しから返された `Unity.Jobs.JobHandle` が破棄される場合 | 依存関係を伝播・結合できるよう、衝突しないローカル変数に代入します |
| `UBP0072` | `Allocator.Persistent` で確保され、未使用かつ未破棄であることを狭い条件で証明できるローカル `NativeArray<T>` | 診断のみ。所有権と破棄位置は開発者が判断します |
| `UBP0073` | `Temp` / `TempJob` の `NativeArray<T>` が return、フィールド保存、または長寿命デリゲートにキャプチャされる場合 | 診断のみ。正しい寿命はアプリケーションに依存します |
| `UBP0074` | 同じ型内で同じ定数を使う `Shader.PropertyToID` の反復呼び出し | 一意な名前の `static readonly` ID フィールドを追加し、反復呼び出しを置換します |
| `UBP0075` | 同じフォルダー内の近隣ファイルに明確な最頻名前空間がある、名前空間のない型ファイル | 近隣ファイルと同じ名前空間で型を囲みます |

アナライザーは Unity のシンボルをセマンティックに解決します。似たメンバー名を持つ無関係な型、未対応のフィールド型、Unity 以外のイテレーター、動的な距離しきい値、生成コード、および必要なシンボルが存在しない Unity／パッケージバージョンは無視します。

パフォーマンス変換には保守的な安全制限があります。スタック割り当ては、プリミティブ型または列挙型の要素について、ループ外かつ合計 1 KiB 以下の場合にのみ提示されます。マネージド配列のゼロ初期化を維持する必要がある場合、修正は `Span.Clear()` を挿入します。`ref` ローカルへの変換には、実際に ref を返すアクセス経路、変化しないレシーバーとインデックス、検出可能な変更、および対応する書き戻し後にコピーしたローカル変数が使われないことが必要です。読み取り専用ジョブフィールドは、ジョブ内のすべての利用が既知の読み取りである場合にだけ提案されます。リスト容量は連続した `Add` 文だけから推測されます。二乗への変換は副作用のないスカラー識別子に限定されます。未初期化 Native メモリは、直後の標準ループが以前の配列内容を読まずにすべてのインデックスへ代入する場合にだけ提示されます。

DOTS の抽出は、直接の `IComponentData` パラメーター、対応する `WithAll`／`WithAny`／`WithNone`／`WithChangeFilter`／クエリオプションフィルター、およびキャプチャしたローカル変数、ネストしたラムダ、システムインスタンスへのアクセスを含まない本体を持つ、セマンティックな Unity Entities 呼び出しだけに提示されます。`ref` パラメーターは `RefRW<T>`、`in` または値パラメーターは `RefRO<T>`、Entity パラメーターは `WithEntityAccess()` へ変換され、クエリフィルターは対応する IJobEntity 属性になります。既存の `SystemAPI.Query` ループでは、アクセス意図を維持して抽出できるよう、ラッパーへ `ValueRW` または `ValueRO` 経由でアクセスする必要があります。

## 意図的に対象外としているもの

このパッケージは、現在の `Microsoft.Unity.Analyzers` カタログ（`UNT0001`～`UNT0043`）と照合済みです。空の Unity メッセージ、`CompareTag`、`TryGetComponent`、割り当てを行わない Physics API、キャッシュされた yield 命令、Transform の位置／回転 API、ループ内での Mesh 配列アクセス、`Animator.StringToHash` など、既存ルールと重複するものは意図的に含めていません。

各ルールは、公式の [Burst コンパイル済みジョブ](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/compilation-burstcompile.html)、[読み取り専用 NativeContainer](https://docs.unity3d.com/Manual/job-system-native-container.html)、[`SystemAPI.Query`](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.SystemAPI.Query.html)、[`IJobEntity` のスケジューリング](https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.IJobEntityExtensions.ScheduleParallel.html)、[`Camera.main` のキャッシュ](https://docs.unity3d.com/ScriptReference/Camera-main.html)、[`NativeArrayOptions.UninitializedMemory`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArrayOptions.UninitializedMemory.html)、[制限付き `stackalloc`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/stackalloc)、[ref を用いた C# のコピー回避](https://learn.microsoft.com/dotnet/csharp/advanced-topics/performance/)のガイダンスに従っています。

## 安全性、インストール、設定

各ルールは `Safe`、`ReviewRequired`、`Experimental` に分類されます。`ReviewRequired` と `Experimental` は Fix All を提供しません。`UBP0001` の修正はソリューション全体の参照解析で外部参照がない場合だけ表示されます。`UBP0058`～`UBP0070` は実行タイミング、同期、依存関係、スレッド安全性、ECS スケジューリングが変わり得るため、1 件ずつ確認してください。詳細は[ルール一覧](docs/rules/index.md)を参照してください。

推奨インストールは GitHub Release の UPM `.tgz` を **Package Manager > Add package from tarball** で追加する方法です。Release には検証済み `.nupkg`、`.snupkg`、標準名 DLL、チェックサムも含まれます。手動 DLL の場合は **Auto Reference**、**Validate References**、全プラットフォームを無効にし、`RoslynAnalyzer` ラベルを設定します。

重大度と保守的なしきい値は `.editorconfig` で設定できます。[設定ガイド](docs/configuration.md)と [`config`](config) のプリセットを参照してください。アナライザーは `netstandard2.0` / Roslyn 3.8 を維持し、Unity の .NET Standard 2.1 プレイヤープロファイルと互換性があります。

## ビルドとテスト

```powershell
dotnet run --project tests/UnityBestPractices.Analyzers.Tests
dotnet pack src/UnityBestPractices.Analyzers -c Release -o artifacts
```

テストは 78 個すべての記述子、修正可能ルール、ドキュメント、Fix All 方針、ソリューション全体の参照、DOTS の全変換先、保守的な除外、パッケージ内容、互換性、および広い性能回帰しきい値を検証します。

### 自動リリース

すべての push と pull request でビルド、全テスト、NuGet/UPM 検証、ドキュメント整合性、性能チェックを実行します。リリースはプロジェクト版と完全一致する SemVer タグ（例 `v0.4.0`）だけから作成されます。再実行しても別バージョンは作られず、DLL、`.nupkg`、`.snupkg`、UPM `.tgz`、`SHA256SUMS` が公開されます。NuGet.org 公開は `NUGET_API_KEY` が設定された場合だけ有効です。

## Unity での使用方法

Unity のプレイヤースクリプティングプロファイルは .NET Standard 2.1 をサポートします。アナライザー DLL は、[Unity の Roslyn アナライザーガイド](https://docs.unity3d.com/2023.2/Documentation/Manual/roslyn-analyzers.html)の要件に従って意図的に `netstandard2.0` を対象としており、.NET Standard 2.1 プレイヤープロファイルを使用するプロジェクトと互換性があります。ソリューションには、公開されている両方のアナライザーエントリポイントを参照する `netstandard2.1` 互換性プロジェクトが含まれます。ローカルおよび CI のすべてのソリューションビルドでこのプロジェクトをコンパイルし、互換性の低下を防ぎます。

1. アナライザーをビルドするか、パッケージ化します。
2. `bin/Release/netstandard2.0`（または NuGet パッケージ内の `analyzers/dotnet/cs` ディレクトリ）にある `UnityBestPractices.Analyzers.dll` を、Unity プロジェクトの `Assets` 以下のフォルダーへコピーします。
3. Unity の Plugin Inspector で DLL を選択します。**Auto Reference**、**Validate References**、**Any Platform**、**Editor**、**Standalone** を無効にし、正確なアセットラベル `RoslynAnalyzer` を割り当てます。
4. Import 設定を適用し、C# プロジェクトを再生成して、Visual Studio または Rider を再起動します。Rider では **Settings | Editor | Inspection Settings | Roslyn Analyzers | Enable Roslyn analyzers** も有効であることを確認します。
5. 薄い点線の提案にキャレットを置き、IDE のクイックアクション（Rider では `Alt+Enter`）を実行します。

Unity はコンパイル時にアナライザーを読み込み、対応 IDE は同じアセンブリからコードフィックスプロバイダーを検出します。`Microsoft.CodeAnalysis.Workspaces` と `System.Composition` はコードフィックスプロバイダーが IDE ホストから受け取る依存関係であるため、Unity の Asset 参照検証は無効のままにします。すべての記述子は `DiagnosticSeverity.Info` を使用し、コンパイラーの警告やエラーではなく、控えめな提案と電球アクションとして表示されます。
