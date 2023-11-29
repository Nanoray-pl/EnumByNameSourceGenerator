using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nanoray.EnumByNameSourceGenerator;

internal static class SyntaxExtractor
{
    public static ClassEnumGeneration? ExtractClass(in GeneratorSyntaxContext context, ClassDeclarationSyntax classDeclarationSyntax, CancellationToken cancellationToken)
    {
        if (!classDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        AccessType accessType = classDeclarationSyntax.GetAccessType();
        if (accessType == AccessType.PRIVATE)
            return null;

        IReadOnlyList<EnumGeneration> attributesForGeneration = GetEnumsToGenerateForClass(context: context, classDeclarationSyntax: classDeclarationSyntax, cancellationToken: cancellationToken);
        if (attributesForGeneration.Count == 0)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        INamedTypeSymbol classSymbol = (INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(declaration: classDeclarationSyntax, cancellationToken: CancellationToken.None)!;
        return new(accessType: accessType, name: classSymbol.Name, classSymbol.ContainingNamespace.ToDisplayString(), enums: attributesForGeneration, classDeclarationSyntax.GetLocation());
    }

    private static IReadOnlyList<EnumGeneration> GetEnumsToGenerateForClass(in GeneratorSyntaxContext context, ClassDeclarationSyntax classDeclarationSyntax, CancellationToken cancellationToken)
    {
        List<EnumGeneration> attributesForGeneration = new();
        INamedTypeSymbol classSymbol = (INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(declaration: classDeclarationSyntax, cancellationToken: cancellationToken)!;
        ImmutableArray<AttributeData> attributes = classSymbol.GetAttributes();

        foreach (AttributeData? item in attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCodeGenerationAttribute(item))
                continue;

            if (item.ConstructorArguments[0].Kind != TypedConstantKind.Type || item.ConstructorArguments[0].Value is not INamedTypeSymbol type)
                continue;

            EnumByNameParseStrategy parseStrategy = EnumByNameParseStrategy.DictionaryCache;
            if (item.ConstructorArguments.Length >= 2 && item.ConstructorArguments[1].Kind == TypedConstantKind.Enum && item.ConstructorArguments[1].Value is int enumValue && Enum.IsDefined(typeof(EnumByNameParseStrategy), (EnumByNameParseStrategy)enumValue))
                parseStrategy = (EnumByNameParseStrategy)enumValue;

            IReadOnlyList<IFieldSymbol> members = type.GetMembers().OfType<IFieldSymbol>().ToArray();
            IReadOnlyList<IFieldSymbol> uniqueMembers = UniqueMembers(members).ToArray();
            EnumGeneration enumGen = new(accessType: AccessType.PUBLIC, name: type.Name, type.ContainingNamespace.ToDisplayString(), members: uniqueMembers, classDeclarationSyntax.GetLocation(), parseStrategy);
            attributesForGeneration.Add(item: enumGen);
        }

        return attributesForGeneration;
    }

    private static IEnumerable<IFieldSymbol> UniqueMembers(IReadOnlyList<IFieldSymbol> members)
    {
        HashSet<string> names = UniqueEnumMemberNames(members);
        foreach (IFieldSymbol member in members)
        {
            if (IsSkipEnumValue(member: member, names: names))
                continue;
            yield return member;
        }
    }

    private static EnumMemberDeclarationSyntax? FindEnumMemberDeclarationSyntax(ISymbol member)
    {
        EnumMemberDeclarationSyntax? syntax = null;
        foreach (SyntaxReference dsr in member.DeclaringSyntaxReferences)
        {
            syntax = GetSyntax(dsr);
            if (syntax is not null)
                break;
        }
        return syntax;
    }

    private static EnumMemberDeclarationSyntax? GetSyntax(SyntaxReference dsr)
        => dsr.GetSyntax(CancellationToken.None) as EnumMemberDeclarationSyntax;

    private static HashSet<string> UniqueEnumMemberNames(IReadOnlyList<IFieldSymbol> members)
        => new(members.Select(m => m.Name).Distinct(StringComparer.Ordinal), comparer: StringComparer.Ordinal);

    private static bool IsSkipEnumValue(IFieldSymbol member, HashSet<string> names)
    {
        EnumMemberDeclarationSyntax? syntax = FindEnumMemberDeclarationSyntax(member);
        if (syntax?.EqualsValue is not null)
        {
            if (syntax.EqualsValue.Value.Kind() == SyntaxKind.IdentifierName)
            {
                bool found = names.Contains(syntax.EqualsValue.Value.ToString());
                if (found)
                {
                    // note deliberately ignoring the return value here as we need to record the integer value as being skipped too
                    IsSkipConstantValue(member: member, names: names);
                    return true;
                }
            }
        }
        return IsSkipConstantValue(member: member, names: names);
    }

    private static bool IsSkipConstantValue(IFieldSymbol member, HashSet<string> names)
    {
        object? cv = member.ConstantValue;
        if (cv is null)
            return false;
        return !names.Add(cv.ToString());
    }

    private static bool IsCodeGenerationAttribute(AttributeData item)
    {
        if (item.AttributeClass is null)
            return false;
        if (item.AttributeClass.ContainingNamespace.ToDisplayString() != "Nanoray.EnumByNameSourceGenerator")
            return false;
        if (item.AttributeClass.Name != "EnumByNameAttribute")
            return false;
        return true;
    }
}
