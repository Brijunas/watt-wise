# Mailpit (Development)

The Development compose stack runs [Mailpit](https://mailpit.axllent.org), an SMTP server that keeps every message it receives instead of delivering it, with a web UI and an API to read them. The Jobs host sends its email here ([backend/docs/email.md](../../backend/docs/email.md)).

## Settings

| 1Password item (`Watt Wise Development`) | Fields                  | Used for                                                                                        |
| ---------------------------------------- | ----------------------- | ----------------------------------------------------------------------------------------------- |
| `mailpit-smtp`                           | `server`, `port`, `url` | Address and port the SMTP server is published on (container port 1025)                          |
| `mailpit`                                | `server`, `port`, `url` | Address and port the web UI and API are published on (container port 8025); `url` is for people |
| `smtp-jobs`                              | `username`, `password`  | The Jobs host's `Smtp__Username` and `Smtp__Password`; Mailpit accepts any credentials          |

- **The compose stack** gets `server` and `port` through `compose.settings.tpl`, rendered into `deploy/.env` (see [postgres.md](postgres.md#passwords)).
- **The Jobs host** gets the SMTP host and port from `appsettings.Development.json`, not from 1Password, and the credentials through its `.env.development`. The `mailpit-smtp` port and `Smtp:Port` there must match.
- **If you change an item,** render `deploy/.env` again and recreate the container.

## Access

Mailpit accepts any username and password (`MP_SMTP_AUTH_ACCEPT_ANY`) over plain SMTP (`MP_SMTP_AUTH_ALLOW_INSECURE`), because the Jobs host always authenticates, as it does against Brevo. The web UI has no login. Neither is a risk while both ports are bound to a loopback address. A remote Development machine is reached through an SSH tunnel, as with PostgreSQL.

## Data

- **Storage:** none. Caught mail is throwaway and is gone when the container is recreated.
- **Health:** the image has its own healthcheck, so `up --wait` waits for it.

## Reading caught emails

- **Web UI:** open the `mailpit` item's `url`. Each message shows its subject, HTML, text and headers.
- **API:** `GET <url>/api/v1/messages` lists them, newest first; `GET <url>/api/v1/message/<id>` returns one. The integration tests read their messages the same way.
