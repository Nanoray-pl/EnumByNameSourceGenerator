using Nanoray.EnumByNameSourceGenerator;

namespace GeneratingTarget;

[EnumByName(typeof(MyEnum), EnumByNameParseStrategy.DictionaryCache)]
internal static partial class MyEnums
{
}
