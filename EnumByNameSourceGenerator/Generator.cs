using System;
using System.Linq;
using Microsoft.CodeAnalysis;

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
            int index = 0;
            foreach (EnumGeneration attribute in classDeclaration.Enums)
            {
                if (index != 0)
                    source.AppendBlankLine();
                GenerateAccessors(source, attribute, classNameFormatter, index);
                index++;
            }
        }
        return className;
    }

    private static CodeBuilder AddUsingDeclarations(CodeBuilder source)
        => AddUsingDeclarations(source: source, "System", "System.CodeDom.Compiler");

    private static CodeBuilder AddUsingDeclarations(CodeBuilder source, params string[] namespaces)
    {
        return namespaces
            .OrderBy(keySelector: n => n, comparer: StringComparer.OrdinalIgnoreCase)
            .Aggregate(seed: source, func: (current, ns) => current.AppendLine($"using {ns};"))
            .AppendBlankLine();
    }

    private static string ClassWithNamespaceFormatter(EnumGeneration d)
        => d.Namespace == "<global namespace>" ? d.Name : $"{d.Namespace}.{d.Name}";

    private static void GenerateAccessors(CodeBuilder source, in EnumGeneration attribute, Func<EnumGeneration, string> classNameFormatter, int index)
    {
        string className = classNameFormatter(attribute);

        switch (attribute.ParseStrategy)
        {
            case EnumByNameParseStrategy.AllOnce:
                {
                    foreach (string member in attribute.Members.Select(member => member.Name))
                        source.AppendLine($"public static readonly {className} {member} = Enum.Parse<{className}>(\"{member}\");");
                }
                break;
            case EnumByNameParseStrategy.EachTime:
                {
                    foreach (string member in attribute.Members.Select(member => member.Name))
                        source.AppendLine($"public static {className} {member} => Enum.Parse<{className}>(\"{member}\");");
                }
                break;
            case EnumByNameParseStrategy.Lazy:
                {
                    foreach (string member in attribute.Members.Select(member => member.Name))
                    {
                        source.AppendLine($"private static readonly Lazy<{className}> __{member} = new Lazy<{className}>(() => Enum.Parse<{className}>(\"{member}\"));");
                        source.AppendLine($"public static {className} {member} => __{member}.Value;");
                    }
                }
                break;
            case EnumByNameParseStrategy.DictionaryCache:
                {
                    source.AppendLine($@"
                        private static readonly Dictionary<string, {className}> __cache{index} = new Dictionary<string, {className}>();

                        private static {className} __ObtainEnumValue{index}(string name)
                        {{
                            {className} enumValue;
                            if (!__cache{index}.TryGetValue(name, out enumValue))
                            {{
                                enumValue = Enum.Parse<{className}>(name);
                                __cache{index}[name] = enumValue;
                            }}
                            return enumValue;
                        }}
                    ");

                    foreach (string member in attribute.Members.Select(member => member.Name))
                        source.AppendLine($"public static {className} {member} => __ObtainEnumValue{index}(\"{member}\");");
                }
                break;
        }
    }

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
