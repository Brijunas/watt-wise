# Domain model and use cases

How business logic is split between Domain and Application, and how it reaches the database. The layers and their references are in [architecture.md](architecture.md#layers-and-dependency-direction); failures in [error-handling.md](error-handling.md). The rules follow Microsoft's DDD guidance where it is still current; the research behind them and where Microsoft is outdated is summed up under [Sources](#sources).

## Where logic goes

| The logic…                                                        | Goes in                                         |
| ----------------------------------------------------------------- | ----------------------------------------------- |
| changes one aggregate's state or guards its rule                  | a method on the aggregate root (Domain)         |
| is a pure business rule over several aggregates or values, no I/O | a domain service (Domain), e.g. the cost engine |
| is the sequence of one user action: load, decide, save, notify    | a command or query handler (Application)        |
| needs I/O to decide (is this email taken?)                        | the handler, before it calls the domain         |
| is a technical detail (hashing, SMTP, EF mapping, Hangfire)       | Infrastructure, behind an Application port      |

Rule of thumb: tell, don't ask. A handler calls `account.AddRole(roleId)`; it never reads `account.RoleIds` to decide on the account's behalf. A service whose methods all take one entity as the first parameter is an entity method in disguise. An entity that needs a repository, logger or `IClock` injected has taken on a handler's job.

## Entities

- **Sealed**, like every class ([conventions.md](conventions.md#code-style)).
- **No public setters.** Properties have `private set`; state changes only through named methods (`AddRole`, `Rename`) or the factory.
- **Creation:** a `public static Create(...)` validates its arguments and returns a valid entity; it generates the `Guid.CreateVersion7()` id. Time comes in as an `Instant` argument, never from a clock inside Domain.
- **One private constructor for EF Core**, commented `// Used by EF Core.`; EF can bind it, it doesn't require it. A second private constructor may take the values `Create` passes. Never `protected`: a sealed class can't have subclasses.
- **Collections:** `private readonly List<T> _items = [];` exposed as `IReadOnlyCollection<T>`. Callers add and remove through the root's methods, never through the collection.
- **Always valid:** a method checks everything before it changes anything, so a failure leaves no partial change. A broken invariant throws a `DomainException` subclass (`Exceptions/`); invalid input is caught earlier, by the request's validator.
- **No persistence in Domain:** no EF attributes, no base class or interface from a framework, no `virtual` members. ASP.NET Core Identity's user (`AccountIdentity`) lives in Infrastructure and points at an `Account` by id.

## Aggregates

- An aggregate is what must be consistent after one save. Its root is the only type the rest of the code changes; child entities are changed through the root.
- **Other aggregates are referenced by id only**, with no navigation property: `Account` holds role ids, `Role` holds permission ids. What a role grants is read by a query, not by walking objects.
- A command changes one aggregate. When a use case must change two atomically, it may, in the same `SaveChangesAsync`; if that becomes routine, the boundary is probably wrong.
- An aggregate is loaded whole (root and children) and saved whole.

## Value objects and enums

- A value object (an amount, a kWh quantity, a time range, a tariff zone) is a `sealed record` or `readonly record struct`, validated when created, compared by value, immutable. No `ValueObject` base class.
- EF Core maps them as complex types (`ComplexProperty`), not owned types.
- A closed set of values is a C# `enum`, stored as text through a converter. A class instead of an enum only when the values carry behaviour.

## Domain services

- Stateless, pure, in Domain, named for what they compute (`CostCalculator`). They take values and aggregates as arguments and return values; no repositories, no I/O.
- Unit-tested in Domain.Tests without a database.

## Use cases

- A handler does four things: load through a repository, check what needs I/O, call the domain, save once through `IUnitOfWork.SaveChangesAsync`. Business rules stay in Domain; a long handler is a sign they didn't.
- Expected failures (not found, conflict, a rule the input breaks) are `Result` errors; a `DomainException` the handler expects is converted ([error-handling.md](error-handling.md#failure-model)).
- Frameworks that live in Infrastructure (Identity, SMTP, Hangfire) reach handlers only through Application ports.

## Persistence ports

Application defines the ports; Infrastructure implements them. Application has no EF Core reference.

- **Repositories:** one per aggregate root, `I<Root>Repository` in the aggregate's Application folder, implemented in `Infrastructure/Persistence/Repositories/`. Methods return whole aggregates or `null` (`GetAsync(id, ct)`), add (`Add`) and remove (`Remove`); there is no `Update`, since loaded aggregates are tracked. Never a generic `IRepository<T>`, never `IQueryable`, never a repository for a child entity.
- **`IUnitOfWork`** (`SaveChangesAsync(ct)`), implemented by `AppDbContext`. One call per command, at the end; that is the transaction.
- **Read ports:** queries don't use repositories or entities. `I<Area>Queries` returns DTO records, implemented in Infrastructure with `AsNoTracking()` and `Select` straight into the DTO. A query may join across aggregates.
- Every port method takes a `CancellationToken`.

## Not now

Added only when a concrete need shows up, each decided first:

- **Domain events.** When a side effect must cross aggregates, a Domain-owned `IDomainEvent` dispatched before `SaveChanges`; never a Mediator type in Domain.
- **A transaction behavior**, when a handler needs several saves or raw SQL next to EF.
- **Strongly typed ids, the Specification pattern, a shared `Entity` base class.**
- **Architecture tests.** The rules here are enforced by project references and review; [architecture.md](architecture.md#layers-and-dependency-direction) says what references allow.

Not used: lazy-loading proxies (they need unsealed classes), owned types for value objects, enumeration-class base types, the EF in-memory provider.

## Sources

- Microsoft: [Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures), [Domain model design](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model), [Domain model with .NET](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model), [Persistence layer design](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design), [CQRS reads](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads).
- Where those eBooks (2018–2022) are outdated, current EF Core docs win: [complex types](https://learn.microsoft.com/en-us/ef/core/modeling/complex-types) over owned types, [constructor binding](https://learn.microsoft.com/en-us/ef/core/modeling/constructors) (no parameterless constructor needed), [testing against a real database](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy).
