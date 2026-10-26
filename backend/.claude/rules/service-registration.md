---
paths:
  - '**/*.cs'
---

# Service registration

- A class library (Application, Infrastructure) has one public entry point: `Add<Project>()` in its `DependencyInjection` class. Everything the library owns is registered there.
- A feature of a library is registered in a private method of that class (Infrastructure's `AddSchemaSteps()` and `AddIdentity()`). Never add a public `Add…()` extension, or a `…ServiceCollectionExtensions` file, for one feature of a library.
- A host's `Program.cs` calls one entry point per library it references and never picks a library's features one by one. Every host gets the whole library; a service it never resolves costs nothing.
- A host's own setup (the Api's CORS, OpenAPI and endpoint modules, the Jobs host's Hangfire storage) stays in extensions in that host's project.

The full rule is "Service registration" in [conventions.md](../../docs/conventions.md#code-style).
