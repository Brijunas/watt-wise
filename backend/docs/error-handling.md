# Backend error handling

How the backend reports failures, from a broken business rule in Domain to the ProblemDetails body a client receives. The request pipeline that carries them is in [architecture.md](architecture.md#request-handling); the system-wide error contract is in the root [technical.md](../../docs/technical.md).

## Failure model

Two kinds of failure, handled two ways:

| Kind       | Examples                                                                | How it travels                                               | HTTP             |
| ---------- | ----------------------------------------------------------------------- | ------------------------------------------------------------ | ---------------- |
| Expected   | Invalid input, a plan that doesn't exist, a user who isn't signed in    | Returned as an `Error` inside `Result<T>`                    | 400, 404, 401, … |
| Unexpected | Database down, a bug, a domain rule broken where no handler expected it | Thrown exception, caught by the exception handler middleware | 500              |

Why:

- **Expected failures are values, not exceptions.** Microsoft's guidance is to handle conditions that happen routinely without exceptions and keep exceptions for the rare, truly exceptional ones ([best practices for exceptions](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)). Throwing is still expensive in .NET 11. A `Result<T>` also makes the failure visible in the handler's signature, and a Hangfire job can tell "nothing to do, don't retry" from a crash that should be retried.
- **Our own `Result<T>`, no library.** It is about a hundred lines and changes rarely; ErrorOr, FluentResults and Ardalis.Result were compared and rejected to keep Application free of dependencies we don't control. C# 15 `union` types were considered too: their one real gain over an enum (the compiler forcing a mapping for every case) is achieved with an exhaustive `switch`, below.
- **Domain throws, Application converts.** Following Robert C. Martin's "use exceptions rather than return codes" (_Clean Code_, ch. 7) literally, Domain reports a broken business rule by throwing a `DomainException` subclass; `Result` lives in Application, which Domain can't reference. A handler catches the domain exceptions it expects and turns them into `Error`s. One it didn't expect is a bug and becomes a 500.
- **Nothing about HTTP below the Api.** Error categories are an Application enum; only the Api knows status codes and ProblemDetails.

## Where each part lives

| Layer       | Types                                                                                       | Job                                                                                 |
| ----------- | ------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Domain      | `Exceptions/DomainException` (abstract, carries a `Code`)                                   | Base class for business-rule violations                                             |
| Application | `Results/Result<T>`, `Results/Error`, `Results/ErrorType`, `Results/IFallibleResult<TSelf>` | The failure vocabulary every handler returns                                        |
| Application | `Behaviors/ValidationBehavior`, `Behaviors/LoggingBehavior`                                 | Turn FluentValidation failures into a `Validation` error; log the outcome           |
| Api         | `ErrorHandling/ErrorTypeMapping`                                                            | The single `ErrorType` → status code table                                          |
| Api         | `ErrorHandling/ResultHttpExtensions`                                                        | `Result<T>.ToHttpResult()`: `200 OK` with the value, or the error as ProblemDetails |
| Api         | `ErrorHandling/ProblemDetailsCustomization`                                                 | Fills `type`, `code` and `traceId` on every ProblemDetails, whatever produced it    |

`Program.cs` wires `AddProblemDetails` with the customization, `UseExceptionHandler()` and `UseStatusCodePages()`. There is no custom `IExceptionHandler`: only unexpected exceptions reach the middleware, and its default ProblemDetails response is what we want. That also keeps their logs and metrics, which .NET 10+ suppresses for exceptions an `IExceptionHandler` reports as handled.

## Error categories

| `ErrorType`    | Status | Factory                                 | Code                  |
| -------------- | ------ | --------------------------------------- | --------------------- |
| `Validation`   | 400    | `Error.Validation(fieldErrors)`         | `Validation.Failed`   |
| `NotFound`     | 404    | `Error.NotFound(code, description)`     | chosen by the handler |
| `Unauthorized` | 401    | `Error.Unauthorized(code, description)` | chosen by the handler |
| (exception)    | 500    | none: thrown                            | `General.Unexpected`  |

Further categories (Conflict, Forbidden, …) are added by the story that first needs them; see below. `ErrorTypeMapping` is a `switch` with no fallback arm and `CS8509` is a build error ([conventions.md](conventions.md#code-style)), so a new `ErrorType` value doesn't compile until it has a status code.

## The contract clients see

Every API error response is `application/problem+json` following [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html):

| Member    | Content                                                                                            |
| --------- | -------------------------------------------------------------------------------------------------- |
| `type`    | Link to the status code's section of RFC 9110                                                      |
| `title`   | The status's standard phrase (`Not Found`), or the framework's validation title                    |
| `status`  | HTTP status code                                                                                   |
| `detail`  | The error's description; never exception messages or stack traces                                  |
| `code`    | Machine-readable code (`Area.Reason`); `General.<Status>` when the framework produced the response |
| `traceId` | The request's trace id, to find its logs                                                           |
| `errors`  | Validation only: camelCase field path → messages                                                   |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "code": "Validation.Failed",
  "errors": { "email": ["'Email' must not be empty."] },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

Responses with no body of their own (routing 404, 405, binding failures) get ProblemDetails from `UseStatusCodePages()` and the same customization. The frontend branches on `code`, never on `title` or `detail`. `/health` is a system check outside this contract and keeps its plain-text body.

## Logging

- **Expected failures:** `LoggingBehavior` logs one Information line per request: `Handled {RequestName} in {ElapsedMs} ms`, or `{RequestName} failed with {ErrorCode} in {ElapsedMs} ms`.
- **Unexpected failures:** the behavior doesn't log exceptions; the exception handler middleware logs each one once at Error, with the stack trace, and the response carries only the `traceId`.

## How to add …

**A new error in an existing category**

1. In the handler, return the factory: `return Error.NotFound("Plan.NotFound", $"Plan {id} does not exist.");`
2. Name the code `Area.Reason` (PascalCase, the area is the aggregate or feature). Codes are part of the API contract: don't rename one once a client uses it.
3. Declare the status on the endpoint for OpenAPI: `.ProducesProblem(StatusCodes.Status404NotFound)`.
4. Cover it in the handler's unit test.

**A new category** (e.g. `Conflict`)

1. Add the value to `ErrorType` and a factory to `Error` (`Error.Conflict(code, description)`).
2. Build: it fails with CS8509 in `ErrorTypeMapping`. Add the status code line there.
3. Add a case for it to `ErrorMappingTests` in Api.IntegrationTests.
4. Add a row to the categories table above.

**A new domain exception**

1. Derive from `DomainException` in the aggregate's folder in Domain, with a stable `Code` (`Area.Reason`) and a message for developers.
2. In each handler that expects it, catch it and return `Error.From(exception, ErrorType.X)`. Don't catch `DomainException` itself: one a handler didn't expect should become a 500.
3. Test the throw in Domain.Tests and the conversion in the handler's unit test.

**A new validator**

1. Next to the request in Application, add `public sealed class XValidator : AbstractValidator<X>`. `AddApplication()` finds it; `ValidationBehavior` runs it.
2. Validate input shape and simple rules only. Rules that need data (does this plan exist?) belong in the handler and return `NotFound` or another category.
3. Unit-test it with `TestValidate` and assert on property names.

**A new use case**

1. Application: request `record` implementing `IQuery<Result<T>>` or `ICommand<Result<T>>`, its handler, and a validator if it takes input.
2. Api: an endpoint in an `IEndpointModule` that sends the request and returns `result.ToHttpResult()`, with `.ProducesProblem(...)` for each status it can return.
3. Tests: handler and validator unit tests; one functional test through `ApiFactory` for the happy path and one error path.
