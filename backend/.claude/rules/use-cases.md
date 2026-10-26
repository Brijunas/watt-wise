---
paths:
  - 'src/WattWise.Application/**/*.cs'
---

# Use cases

- A handler loads through a repository, checks what needs I/O, calls the domain, and saves once with `IUnitOfWork.SaveChangesAsync`. No business rules in handlers.
- Ports live here: one `I<Root>Repository` per aggregate root (whole aggregates, no `IQueryable`, never generic), `IUnitOfWork`, and `I<Area>Queries` read ports returning DTO records. Queries never return entities.
- No EF Core types in Application. Identity, SMTP and Hangfire are reached through ports.
- Expected failures are `Result` errors; convert only the `DomainException`s the handler expects.

The full rules are in [domain-model.md](../../docs/domain-model.md#use-cases).
