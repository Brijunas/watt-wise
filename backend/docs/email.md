# Email

How the backend sends transactional email: the port, the messages, rendering, the settings and how to try it. Why email is sent directly over SMTP is in [technical.md](../../docs/technical.md#cross-cutting); the rules it follows are in [security.md](../../docs/security.md). Which message each account flow sends is in [features/accounts.md](features/accounts.md#emails).

## The port and the messages

- **Port:** Application's `Email/IEmailSender` has one method, `SendAsync(EmailMessage, CancellationToken)`. The folder is named for the concept because no single feature owns email.
- **Messages:** `EmailMessage(To, Language)` is the abstract record in `Email/`. Each kind is a record in its feature's folder that adds its own values, such as the link. `Accounts/` has `EmailConfirmationEmail`, `AccountAlreadyExistsEmail` and `PasswordResetEmail`.
- **Language:** the caller picks the language, normally the account's preferred one. `Language.Lt` and `Language.En` have a template each.
- **Adapter:** Infrastructure's `Email/SmtpEmailSender` renders the message with `EmailRenderer` and sends it with MailKit, on a new connection per message. `AddInfrastructure()` registers both through a private `AddEmail()`, so every host has them; Jobs and the Cli need no `Smtp` settings unless they send.

## Rendering

Fluid 2.40 (Liquid) fills the placeholders and Mjml.Net 4.15 compiles the HTML part into responsive, email-safe HTML. Both are MIT, maintained, free and widely used. Liquid is safe because a template can't run code.

`EmailRenderer` is a singleton that parses every template once and caches it. For one message it:

1. renders the subject and the text part with Fluid, unencoded;
2. renders the kind's MJML body with Fluid, HTML-encoding every value;
3. puts that body into `_Layout.mjml.liquid` through `body | raw`, so it is not encoded twice;
4. compiles the result with MJML.

An MJML error throws, so a broken template fails a test, not a user.

## Templates

They are embedded resources under `Infrastructure/Email/Templates/`:

```text
_Layout.mjml.liquid           header and footer shared by every email
<Kind>/<lang>.subject.liquid  one line
<Kind>/<lang>.text.liquid     plain-text part
<Kind>/<lang>.mjml.liquid     the body, inside the layout's mj-column
```

`<Kind>` is `EmailConfirmation`, `AccountAlreadyExists` or `PasswordReset`; `<lang>` is `lt` or `en`. Every template can use `lang`, `footer` and the message's own values (`link`).

### How to add an email

1. Add a record that derives from `EmailMessage` in the feature's Application folder.
2. Add the six template files for the kind, in LT and EN. Say what happened, give the link with its lifetime ([security.md](../../docs/security.md#accounts)) and tell the reader to ignore the email if they didn't ask for it.
3. Add a case for the record to `EmailRenderer` with its kind name and values.
4. Add the kind to the renderer and sender tests (below).
5. Add it to the Emails table in the feature's doc.

## Settings

The `Smtp` section is deliberately split. The non-secret connection settings are in `appsettings.json` and `appsettings.<Environment>.json`; only the credentials come from 1Password, through the Api's `.env.<environment>` file.

| Key                | Comes from                 | Development value             |
| ------------------ | -------------------------- | ----------------------------- |
| `Smtp:Host`        | `appsettings.Development`  | `127.0.0.1`                   |
| `Smtp:Port`        | `appsettings.Development`  | `1025`                        |
| `Smtp:Security`    | `appsettings.Development`  | `None`                        |
| `Smtp:FromAddress` | `appsettings.Development`  | `no-reply@wattwise.localhost` |
| `Smtp:FromName`    | `appsettings.json`         | `Watt-Wise`                   |
| `Smtp:Username`    | 1Password, `smtp-api` item | any value; Mailpit accepts it |
| `Smtp:Password`    | 1Password, `smtp-api` item | any value; Mailpit accepts it |

- **Must match:** the SMTP port is in two places in Development: the `mailpit-smtp` item (the port compose publishes) and `appsettings.Development.json`. Change both together ([mailpit.md](../../deploy/docs/mailpit.md)).
- **Validation:** `SmtpSettings` reads the section and lists the missing or invalid keys. Only the Api's `Program.cs` adds `AddOptions<SmtpSettings>().ValidateOnStart()`, so the Api stops at start with `Smtp configuration is missing or invalid: Smtp:Password, …`. The message holds key names, never values. Jobs and the Cli validate the settings only when they first send.
- **No encryption only in Development:** the validator also reports `Smtp:Security` when it is `None` and the host's environment isn't Development, and a port above 65535 counts as invalid.
- **Credentials everywhere:** the username and password are required in every environment, Development included.
- **Password:** `SmtpSettings` is a class, not a record, so `ToString` can't print the password.

### TLS modes

| `Smtp:Security` | Connection                         | Port | Used by                                                        |
| --------------- | ---------------------------------- | ---- | -------------------------------------------------------------- |
| `StartTls`      | plain, then upgraded with STARTTLS | 587  | the default in `appsettings.json`                              |
| `SslOnConnect`  | TLS from the first byte            | 465  | when the provider wants it                                     |
| `None`          | no encryption                      | any  | Development's Mailpit only; the validator refuses it elsewhere |

The default is `StartTls`, so an environment that forgets the key doesn't send in plain text ([security.md](../../docs/security.md#transport-and-limits)).

## What goes on the wire

- **Message-Id and EHLO:** `MessageId` is generated for the from address's domain and `SmtpClient.LocalDomain` is that domain too, so neither reveals the machine's hostname ([security.md](../../docs/security.md#deployment)).
- **Body:** a `multipart/alternative` with the text and the HTML part.

## Failure handling

A failed send is logged and doesn't throw, because the user can ask for the email again.

- **What is logged:** at Error, the email kind, the language and the exception's type name (`SocketException`, `AuthenticationException`, …); a server refusal (`SmtpCommandException`) adds its SMTP status code. Never the recipient and never the exception object or its message: a server's reply often echoes the address (`550 <user@x> unknown`), and a message can name the host or username.
- **What is caught:** SMTP, TLS, authentication, network and timeout failures, and `NotSupportedException` (the server offers no AUTH or no STARTTLS). Cancellation propagates, and so does a bug such as a broken template, a missing setting or a recipient that isn't an address (callers pass the account's normalized, validated email).
- **Timeout:** every client operation has a 30-second timeout (`TimeoutMilliseconds` in `SmtpEmailSender`) instead of MailKit's two minutes, so a stalled server doesn't hold a request. A timeout is logged like any failure.
- A success is logged at Debug.

## Telemetry

`AddInfrastructure()` switches on MailKit's own telemetry, once per process. Its sources and meters, `mailkit.net.smtp` and `mailkit.net.socket`, are in the shared setup ([observability.md](observability.md#shared-setup)). MailKit 4.18's SMTP spans carry only `url.scheme`, `server.address`, `server.port`, `network.peer.address`, `network.operation` (connect, authenticate or send) and `error.type` (the exception's type name). They hold no recipient, no server reply and no exception message, so the activity source is exported; a test pins that.

## Testing

- **Unit (Infrastructure.Tests):** the settings and their validator; the renderer for every kind and language (subject, text body, link in both parts with the right escaping, `lang` and footer); and the sender's failure paths against `ScriptedSmtpServer`, a small in-process SMTP server that scripts one behavior (rejects the recipient or the credentials, offers no AUTH or STARTTLS, drops the connection, stalls). They check the log entry, that nothing sensitive is in it, what the client sent (EHLO domain, AUTH before the message, QUIT) and that cancellation propagates.
- **Integration (Infrastructure.IntegrationTests):** `Email/SmtpEmailSenderTests` sends a kind in each language through `IEmailSender` to a Mailpit container and reads it back through Mailpit's API: recipient, subject, the link in both parts and a Message-Id on the from domain. The container is `MailpitContainerFixture` in `WattWise.Testing` ([testing.md](testing.md#shared-mailpit-fixture)).
- **Api.IntegrationTests:** every host start gets dummy `Smtp` settings, and a blank section stops the host.
- **By hand:** start the Development stack and the Api, send an email and read it in Mailpit's web UI ([mailpit.md](../../deploy/docs/mailpit.md#reading-caught-emails)).
