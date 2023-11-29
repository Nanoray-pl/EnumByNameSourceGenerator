using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nanoray.EnumByNameSourceGenerator;

[Generator(LanguageNames.CSharp)]
public class EnumByNameSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(ExtractClasses(context), action: GenerateClasses);

        context.RegisterPostInitializationOutput(i =>
        {
            string attributeSource = @"
                using System;

                namespace Nanoray.EnumByNameSourceGenerator;

                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
                public sealed class EnumByNameAttribute : Attribute
                {
                    public EnumByNameAttribute(Type enumType)
                    {
                        this.Enum = enumType;
                        if (!enumType.IsEnum)
                            throw new ArgumentException(message: ""The type must be an enum."", nameof(enumType));
                    }

                    public Type Enum { get; }
                }
            ";
            i.AddSource("EnumByNameAttribute.g.cs", attributeSource);
        });
    }

    private static IncrementalValuesProvider<ClassEnumGeneration?> ExtractClasses(in IncrementalGeneratorInitializationContext context)
        => context.SyntaxProvider.CreateSyntaxProvider(predicate: static (n, _) => n is ClassDeclarationSyntax, transform: GetClassDetails);

    private static ClassEnumGeneration? GetClassDetails(GeneratorSyntaxContext generatorSyntaxContext, CancellationToken cancellationToken)
        => generatorSyntaxContext.Node is ClassDeclarationSyntax classDeclarationSyntax
            ? SyntaxExtractor.ExtractClass(context: generatorSyntaxContext, classDeclarationSyntax: classDeclarationSyntax, cancellationToken: cancellationToken)
            : null;

    private static void GenerateClasses(SourceProductionContext sourceProductionContext, ClassEnumGeneration? classEnumGeneration)
    {
        if (classEnumGeneration is null)
            return;

        string className = Generator.GenerateClassForClass(classDeclaration: classEnumGeneration.Value, out CodeBuilder? codeBuilder);
        sourceProductionContext.AddSource(classEnumGeneration.Value.Namespace + "." + className + ".generated.cs", sourceText: codeBuilder.Text);
    }
}
