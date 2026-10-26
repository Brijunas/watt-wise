---
paths:
  - 'src/WattWise.Infrastructure/Persistence/**/*.cs'
---

# Persistence

- Mapping only in `IEntityTypeConfiguration<T>` classes in `Configurations/`; nothing persistence-related in Domain.
- Relations between aggregates are foreign-key ids, never navigations. Value objects are complex types (`ComplexProperty`), not owned types. Enums are stored as text.
- Repositories (`Repositories/`) load the whole aggregate and return it or `null`; read ports use `AsNoTracking()` and project with `Select` into the DTO.
- `AppDbContext` implements `IUnitOfWork`; repositories never call `SaveChangesAsync`.
- No lazy-loading proxies.

The full rules are in [domain-model.md](../../docs/domain-model.md#persistence-ports).
