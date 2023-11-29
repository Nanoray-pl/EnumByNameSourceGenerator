# EnumByNameSourceGenerator

A source generator which given a class attributed with `[EnumByName(Type enumType)]` generates static accessors to that enum's cases using their names instead of values.

Big thanks to the author of the https://github.com/credfeto/credfeto-enum-source-generation package. The code mostly comes from that project, but repurposed to this specific task.