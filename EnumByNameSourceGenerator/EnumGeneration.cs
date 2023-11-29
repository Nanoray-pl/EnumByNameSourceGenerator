using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Nanoray.EnumByNameSourceGenerator;

[DebuggerDisplay("{AccessType} {Namespace}.{Name}")]
public readonly record struct EnumGeneration
{
    public EnumGeneration(AccessType accessType, string name, string @namespace, IReadOnlyList<IFieldSymbol> members, Location location, EnumByNameParseStrategy parseStrategy)
    {
        this.AccessType = accessType;
        this.Name = name;
        this.Namespace = @namespace;
        this.Members = members;
        this.Location = location;
        this.ParseStrategy = parseStrategy;
    }

    public AccessType AccessType { get; }
    public string Name { get; }
    public string Namespace { get; }
    public IReadOnlyList<IFieldSymbol> Members { get; }
    public Location Location { get; }
    public EnumByNameParseStrategy ParseStrategy { get; }
}
