// IJobEntity Run/Schedule/ScheduleParallel execution model.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers;

internal sealed class JobEntityExecution
{
    private JobEntityExecution(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        string mode)
    {
        Invocation = invocation;
        MemberAccess = memberAccess;
        Mode = mode;
    }

    internal InvocationExpressionSyntax Invocation { get; }

    internal MemberAccessExpressionSyntax MemberAccess { get; }

    internal string Mode { get; }

    internal static bool TryCreate(
        ExpressionStatementSyntax statement,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out JobEntityExecution execution)
    {
        execution = null!;
        if (statement.Expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.Count != 0 ||
            invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var mode = access.Name.Identifier.ValueText;
        if (mode != "Run" && mode != "Schedule" && mode != "ScheduleParallel")
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var receiverType = semanticModel.GetTypeInfo(access.Expression, cancellationToken).Type;
        var jobEntity = UnitySymbolCache.GetTypeByMetadataName(
            semanticModel.Compilation,
            "Unity.Entities.IJobEntity");
        if (!DotsQuerySemanticHelpers.IsUnityEntitiesMethod(method, mode) ||
            receiverType is not INamedTypeSymbol namedReceiver ||
            jobEntity is null ||
            !namedReceiver.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, jobEntity)))
        {
            return false;
        }

        execution = new JobEntityExecution(invocation, access, mode);
        return true;
    }
}

