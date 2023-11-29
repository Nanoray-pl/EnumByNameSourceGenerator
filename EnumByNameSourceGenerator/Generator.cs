using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nanoray.EnumByNameSourceGenerator;

internal static class Generator
{
    public static string GenerateClassForClass(in ClassEnumGeneration classDeclaration, out CodeBuilder source)
    {
        string className = classDeclaration.Name;

        source = AddUsingDeclarations(new());
        if (classDeclaration.Namespace != "<global namespace>")
            source = source.AppendLine($"namespace {classDeclaration.Namespace};").AppendBlankLine();
        source = source.AppendLine($"[GeneratedCode(tool: \"{typeof(EnumByNameSourceGenerator).FullName}\", version: \"1.0.0\")]");

        using (source.StartBlock($"{ConvertAccessType(classDeclaration.AccessType)} static partial class {className}"))
        {
            Func<EnumGeneration, string> classNameFormatter = ClassWithNamespaceFormatter;
            bool isFirst = true;
            foreach (EnumGeneration attribute in classDeclaration.Enums)
            {
                if (isFirst)
                    isFirst = false;
                else
                    source.AppendBlankLine();

                GenerateAccessors(source, attribute, classNameFormatter);
            }
        }
        return className;
    }

    private static CodeBuilder AddUsingDeclarations(CodeBuilder source)
        => AddUsingDeclarations(source: source, "System.CodeDom.Compiler");

    private static CodeBuilder AddUsingDeclarations(CodeBuilder source, params string[] namespaces)
    {
        return namespaces
            .OrderBy(keySelector: n => n, comparer: StringComparer.OrdinalIgnoreCase)
            .Aggregate(seed: source, func: (current, ns) => current.AppendLine($"using {ns};"))
            .AppendBlankLine();
    }

    private static string ClassWithNamespaceFormatter(EnumGeneration d)
        => d.Namespace == "<global namespace>" ? d.Name : $"{d.Namespace}.{d.Name}";

    private static void GenerateAccessors(CodeBuilder source, in EnumGeneration attribute, Func<EnumGeneration, string> classNameFormatter)
    {
        string className = classNameFormatter(attribute);
        foreach (string member in UniqueMembers(attribute).Select(member => member.Name))
            source.AppendLine($"public static readonly {className} {member} = Enum.Parse<{className}>(\"{member}\");");
    }

    private static IEnumerable<IFieldSymbol> UniqueMembers(EnumGeneration enumDeclaration)
    {
        HashSet<string> names = UniqueEnumMemberNames(enumDeclaration);
        foreach (IFieldSymbol member in enumDeclaration.Members)
        {
            if (IsSkipEnumValue(member: member, names: names))
                continue;
            yield return member;
        }
    }

    private static HashSet<string> UniqueEnumMemberNames(in EnumGeneration enumDeclaration)
        => new(enumDeclaration.Members.Select(m => m.Name).Distinct(StringComparer.Ordinal), comparer: StringComparer.Ordinal);

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

    private static string ConvertAccessType(AccessType accessType)
        => accessType switch
        {
            AccessType.PUBLIC => "public",
            AccessType.PRIVATE => "private",
            AccessType.PROTECTED => "protected",
            AccessType.PROTECTED_INTERNAL => "protected internal",
            AccessType.INTERNAL => "internal",
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), actualValue: accessType, message: "Unknown access type")
        };
}
