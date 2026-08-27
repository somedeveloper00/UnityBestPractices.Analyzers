using UnityBestPractices.Analyzers;
using UnityBestPractices.Analyzers.Infrastructure;
// Shared parameter, field, disposal, and filter models for DOTS conversions.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityBestPractices.Analyzers.Rules.Dots;

internal enum DotsParameterAccess
{
    ReadOnly,
    ReadWrite,
    BufferReadOnly,
    BufferReadWrite,
    Entity,
    EntityIndexInQuery,
}

internal sealed class DotsQueryParameter
{
    internal DotsQueryParameter(
        string name,
        string typeName,
        DotsParameterAccess access,
        ISymbol symbol)
    {
        Name = name;
        TypeName = typeName;
        Access = access;
        Symbol = symbol;
    }

    internal string Name { get; }

    internal string TypeName { get; }

    internal DotsParameterAccess Access { get; }

    internal ISymbol Symbol { get; }

    internal string JobParameter => Access switch
    {
        DotsParameterAccess.ReadWrite or DotsParameterAccess.BufferReadWrite =>
            "ref " + TypeName + " " + Name,
        DotsParameterAccess.ReadOnly or DotsParameterAccess.BufferReadOnly =>
            "in " + TypeName + " " + Name,
        DotsParameterAccess.EntityIndexInQuery =>
            "[Unity.Entities.EntityIndexInQuery] int " + Name,
        _ => TypeName + " " + Name,
    };

    internal string SystemApiType => Access switch
    {
        DotsParameterAccess.ReadWrite => "Unity.Entities.RefRW<" + TypeName + ">",
        DotsParameterAccess.ReadOnly => "Unity.Entities.RefRO<" + TypeName + ">",
        DotsParameterAccess.BufferReadOnly or DotsParameterAccess.BufferReadWrite => TypeName,
        _ => string.Empty,
    };
}

internal sealed class DotsJobField
{
    internal DotsJobField(
        string name,
        string typeName,
        string initializer,
        ISymbol? sourceSymbol = null,
        string? preJobDeclaration = null)
    {
        Name = name;
        TypeName = typeName;
        Initializer = initializer;
        SourceSymbol = sourceSymbol;
        PreJobDeclaration = preJobDeclaration;
    }

    internal string Name { get; }

    internal string TypeName { get; }

    internal string Initializer { get; }

    internal ISymbol? SourceSymbol { get; }

    // A declaration which must be evaluated by the system before constructing the job.
    internal string? PreJobDeclaration { get; }

    internal bool IsReadOnly { get; set; }
}

internal sealed class DotsDisposalCapture
{
    internal DotsDisposalCapture(ISymbol symbol, DotsJobField? jobField, string expression)
    {
        Symbol = symbol;
        JobField = jobField;
        Expression = expression;
    }

    internal ISymbol Symbol { get; }

    internal DotsJobField? JobField { get; }

    internal string Expression { get; }
}

internal sealed class DotsQueryFilter
{
    internal DotsQueryFilter(string name, ImmutableArray<string> typeNames, string? argument)
    {
        Name = name;
        TypeNames = typeNames;
        Argument = argument;
    }

    internal string Name { get; }

    internal ImmutableArray<string> TypeNames { get; }

    internal string? Argument { get; }

    internal string ToSystemApiSuffix()
    {
        if (Name == "WithEntityQueryOptions")
        {
            return ".WithOptions(" + Argument + ")";
        }

        return "." + Name + "<" + string.Join(", ", TypeNames) + ">()";
    }

    internal string ToJobAttribute()
    {
        if (Name == "WithEntityQueryOptions")
        {
            return "[Unity.Entities.WithOptions(" + Argument + ")]\n";
        }

        return
            "[Unity.Entities." + Name +
            "(" + string.Join(", ", TypeNames.Select(type => "typeof(" + type + ")")) + ")]\n";
    }
}

