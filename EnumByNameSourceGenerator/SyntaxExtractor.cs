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

            TypedConstant constructorArguments = item.ConstructorArguments[0];
            if (constructorArguments.Kind == TypedConstantKind.Type && constructorArguments.Value is INamedTypeSymbol type)
            {
                IReadOnlyList<IFieldSymbol> members = type.GetMembers().OfType<IFieldSymbol>().ToArray();
                EnumGeneration enumGen = new(accessType: AccessType.PUBLIC, name: type.Name, type.ContainingNamespace.ToDisplayString(), members: members, classDeclarationSyntax.GetLocation());
                attributesForGeneration.Add(item: enumGen);
            }
        }

        return attributesForGeneration;
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
