using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using UnityBestPractices.Analyzers;

internal sealed partial class AnalyzerTests
{
    private async Task VerifyExpressionQuickFixesAsync()
    {
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(0f, 0f)", DiagnosticIds.UseVector2Zero, "UnityEngine.Vector2.zero");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(1f, 1f)", DiagnosticIds.UseVector2One, "UnityEngine.Vector2.one");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(0f, 1f)", DiagnosticIds.UseVector2Up, "UnityEngine.Vector2.up");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(0f, -1f)", DiagnosticIds.UseVector2Down, "UnityEngine.Vector2.down");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(-1f, 0f)", DiagnosticIds.UseVector2Left, "UnityEngine.Vector2.left");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector2(1f, 0f)", DiagnosticIds.UseVector2Right, "UnityEngine.Vector2.right");

        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 0f, 0f)", DiagnosticIds.UseVector3Zero, "UnityEngine.Vector3.zero");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(1f, 1f, 1f)", DiagnosticIds.UseVector3One, "UnityEngine.Vector3.one");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 1f, 0f)", DiagnosticIds.UseVector3Up, "UnityEngine.Vector3.up");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, -1f, 0f)", DiagnosticIds.UseVector3Down, "UnityEngine.Vector3.down");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(-1f, 0f, 0f)", DiagnosticIds.UseVector3Left, "UnityEngine.Vector3.left");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(1f, 0f, 0f)", DiagnosticIds.UseVector3Right, "UnityEngine.Vector3.right");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 0f, 1f)", DiagnosticIds.UseVector3Forward, "UnityEngine.Vector3.forward");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Vector3(0f, 0f, -1f)", DiagnosticIds.UseVector3Back, "UnityEngine.Vector3.back");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Quaternion(0f, 0f, 0f, 1f)", DiagnosticIds.UseQuaternionIdentity, "UnityEngine.Quaternion.identity");
        await VerifyExpressionFixAsync("using UnityEngine;", "Quaternion.Euler(0f, 0f, 0f)", DiagnosticIds.UseQuaternionIdentityForEulerZero, "UnityEngine.Quaternion.identity");

        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 0f, 0f, 0f)", DiagnosticIds.UseColorClear, "UnityEngine.Color.clear");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 0f, 0f, 1f)", DiagnosticIds.UseColorBlack, "UnityEngine.Color.black");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 1f, 1f, 1f)", DiagnosticIds.UseColorWhite, "UnityEngine.Color.white");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 0f, 0f, 1f)", DiagnosticIds.UseColorRed, "UnityEngine.Color.red");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 1f, 0f, 1f)", DiagnosticIds.UseColorGreen, "UnityEngine.Color.green");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 0f, 1f, 1f)", DiagnosticIds.UseColorBlue, "UnityEngine.Color.blue");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 0.9215686f, 0.01568628f, 1f)", DiagnosticIds.UseColorYellow, "UnityEngine.Color.yellow");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(0f, 1f, 1f, 1f)", DiagnosticIds.UseColorCyan, "UnityEngine.Color.cyan");
        await VerifyExpressionFixAsync("using UnityEngine;", "new Color(1f, 0f, 1f, 1f)", DiagnosticIds.UseColorMagenta, "UnityEngine.Color.magenta");

        await VerifyExpressionFixAsync("using UnityEngine;", "Mathf.Clamp(2f, 0f, 1f)", DiagnosticIds.UseMathfClamp01, "UnityEngine.Mathf.Clamp01(2f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "Mathf.Pow(4f, 0.5f)", DiagnosticIds.UseMathfSqrt, "UnityEngine.Mathf.Sqrt(4f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "(int)Mathf.Floor(2.5f)", DiagnosticIds.UseMathfFloorToInt, "UnityEngine.Mathf.FloorToInt(2.5f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "(int)Mathf.Ceil(2.5f)", DiagnosticIds.UseMathfCeilToInt, "UnityEngine.Mathf.CeilToInt(2.5f)");
        await VerifyExpressionFixAsync("using UnityEngine;", "(int)Mathf.Round(2.5f)", DiagnosticIds.UseMathfRoundToInt, "UnityEngine.Mathf.RoundToInt(2.5f)");

        await VerifyExpressionFixAsync(string.Empty, "new int[0]", DiagnosticIds.UseArrayEmpty, "System.Array.Empty<int>()");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).Any()", DiagnosticIds.FuseWhereAny, "new[] { 1, 2 }.Any(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).Count()", DiagnosticIds.FuseWhereCount, "new[] { 1, 2 }.Count(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).First()", DiagnosticIds.FuseWhereFirst, "new[] { 1, 2 }.First(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1, 2 }.Where(item => item > 0).FirstOrDefault()", DiagnosticIds.FuseWhereFirstOrDefault, "new[] { 1, 2 }.FirstOrDefault(item => item > 0)");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new Dictionary<int, int>().Keys.Contains(1)", DiagnosticIds.UseDictionaryContainsKey, "new Dictionary<int, int>().ContainsKey(1)");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new List<int> { 1 }.ElementAt(0)", DiagnosticIds.UseListIndexer, "new List<int> { 1 }[0]");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new List<int> { 1 }.Count()", DiagnosticIds.UseListCountProperty, "new List<int> { 1 }.Count");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1 }.Count()", DiagnosticIds.UseArrayLengthProperty, "new[] { 1 }.Length");
        await VerifyExpressionFixAsync("using System.Linq;", "new[] { 1 }.Any()", DiagnosticIds.UseArrayLengthForAny, "new[] { 1 }.Length != 0");
        await VerifyExpressionFixAsync("using System.Collections.Generic; using System.Linq;", "new List<int> { 1 }.Any()", DiagnosticIds.UseListCountForAny, "new List<int> { 1 }.Count != 0");
        await VerifyFixAsync(
            "using System.Linq;\nclass NegatedArrayAny { bool Run(int[] values) { return !values.Any(); } }",
            DiagnosticIds.UseArrayLengthForAny,
            "using System.Linq;\nclass NegatedArrayAny { bool Run(int[] values) { return !(values.Length != 0); } }");
        await VerifyFixAsync(
            "using System.Collections.Generic; using System.Linq;\nclass NegatedListAny { bool Run(List<int> values) { return !values.Any(); } }",
            DiagnosticIds.UseListCountForAny,
            "using System.Collections.Generic; using System.Linq;\nclass NegatedListAny { bool Run(List<int> values) { return !(values.Count != 0); } }");
        await VerifyFixAsync(
            "using System.Linq;\nclass ComparedArrayAny { bool Run(int[] values, bool expected) { return expected == values.Any(); } }",
            DiagnosticIds.UseArrayLengthForAny,
            "using System.Linq;\nclass ComparedArrayAny { bool Run(int[] values, bool expected) { return expected == (values.Length != 0); } }");
        await VerifyFixAsync(
            "using System.Collections.Generic; using System.Linq;\nclass ConditionalListAny { int Run(List<int> values) { return values.Any() && values[0] > 0 ? 1 : 0; } }",
            DiagnosticIds.UseListCountForAny,
            "using System.Collections.Generic; using System.Linq;\nclass ConditionalListAny { int Run(List<int> values) { return (values.Count != 0) && values[0] > 0 ? 1 : 0; } }");
        await VerifyExpressionFixAsync("using System.Text;", "new StringBuilder().Append(\"x\")", DiagnosticIds.AppendCharacter, "new StringBuilder().Append('x')");
        await VerifyExpressionFixAsync("using System.Text;", "new StringBuilder().AppendLine(\"\")", DiagnosticIds.AppendLineWithoutEmptyString, "new StringBuilder().AppendLine()");
        await VerifyExpressionFixAsync("using System.Threading;", "new CancellationToken()", DiagnosticIds.UseCancellationTokenNone, "System.Threading.CancellationToken.None");
        await VerifyExpressionFixAsync("using System;", "new Guid()", DiagnosticIds.UseGuidEmpty, "System.Guid.Empty");
        await VerifyExpressionFixAsync("using System.Linq;", "Enumerable.Empty<int>().ToArray()", DiagnosticIds.UseArrayEmptyForEnumerableEmpty, "System.Array.Empty<int>()");
    }

    private async Task VerifyExpressionFixAsync(
        string usingDirectives,
        string expression,
        string diagnosticId,
        string expectedExpression)
    {
        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixCase { object Run() => " + expression + "; }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixCase { object Run() => " + expectedExpression + "; }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixReturnCase { object Run() { return " + expression + "; } }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixReturnCase { object Run() { return " + expectedExpression + "; } }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixParenthesizedCase { object Run() { return (" + expression + "); } }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixParenthesizedCase { object Run() { return (" + expectedExpression + "); } }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixLocalCase { object Run() { object value = " + expression + "; return value; } }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixLocalCase { object Run() { object value = " + expectedExpression + "; return value; } }");

        await VerifyFixAsync(
            usingDirectives + "\nclass ExpressionQuickFixArgumentCase { object Run() { return Identity(" + expression + "); } object Identity(object value) => value; }",
            diagnosticId,
            usingDirectives + "\nclass ExpressionQuickFixArgumentCase { object Run() { return Identity(" + expectedExpression + "); } object Identity(object value) => value; }");
    }

}
