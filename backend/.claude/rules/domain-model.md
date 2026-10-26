---
paths:
  - 'src/WattWise.Domain/**/*.cs'
---

# Domain code

- Entities: sealed, `private set` only, state changed through named methods, `static Create` that validates and makes the `Guid.CreateVersion7()` id, one private constructor commented `// Used by EF Core.`, collections as `private readonly List<T>` exposed as `IReadOnlyCollection<T>`.
- Check everything before changing anything; a broken invariant throws a `DomainException` subclass.
- Other aggregates by id only: no navigation property to another aggregate root.
- Value objects: `sealed record` or `readonly record struct`, validated on creation, no base class.
- Domain services: stateless and pure. No repositories, `IClock`, logging or I/O anywhere in Domain; time comes in as an argument.
- No EF, ASP.NET Core, Mediator or FluentValidation types in Domain.

The full rules are in [domain-model.md](../../docs/domain-model.md).
