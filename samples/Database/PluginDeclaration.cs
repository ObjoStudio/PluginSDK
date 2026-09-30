using Objo.Runtime.Abstractions;

[assembly: ObjoPlugin(
    "acme.database",
    "1.0.0",
    Namespace = "Acme.Database",
    DisplayName = "Acme Database",
    Description = "A SQLite provider wrapper with typed results, parameters and transactions.",
    Publisher = "Acme Tools Ltd",
    Licence = "Apache-2.0")]
