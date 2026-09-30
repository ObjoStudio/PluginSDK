using Objo.Runtime.Abstractions;

[assembly: ObjoPlugin("acme.mutator", "1.0.0",
    Namespace = "Acme.Mutator", DisplayName = "Acme Mutator",
    Description = "Changes a storage object created by another plugin.",
    Publisher = "Acme Tools Ltd", Licence = "MIT")]
