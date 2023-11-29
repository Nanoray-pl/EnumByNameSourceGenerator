using Microsoft.CodeAnalysis.CSharp;

namespace Nanoray.EnumByNameSourceGenerator;

public enum AccessType
{
    PUBLIC = SyntaxKind.PublicKeyword,
    PRIVATE = SyntaxKind.PrivateKeyword,
    PROTECTED = SyntaxKind.ProtectedKeyword,
    PROTECTED_INTERNAL = SyntaxKind.ProtectedKeyword | SyntaxKind.InternalKeyword,
    INTERNAL = SyntaxKind.InternalKeyword
}
