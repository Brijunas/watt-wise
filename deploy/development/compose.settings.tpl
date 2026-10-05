# Template for deploy/.env, the non-secret settings of the Development compose stack.
# Docker Compose reads deploy/.env automatically, so everyday compose commands need no
# op run. Render it after setup and whenever one of these 1Password values changes:
#
#   op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env
#
# Passwords are never rendered; they stay in compose.env (op run, first start only).
# Vault: "Watt Wise Development", items postgres-admin and pgadmin.
POSTGRES_BIND_ADDRESS="{{ op://Watt Wise Development/postgres-admin/server }}"
POSTGRES_PORT="{{ op://Watt Wise Development/postgres-admin/port }}"
POSTGRES_USER="{{ op://Watt Wise Development/postgres-admin/username }}"
POSTGRES_DB="{{ op://Watt Wise Development/postgres-admin/database }}"
PGADMIN_BIND_ADDRESS="{{ op://Watt Wise Development/pgadmin/server }}"
PGADMIN_PORT="{{ op://Watt Wise Development/pgadmin/port }}"
PGADMIN_DEFAULT_EMAIL="{{ op://Watt Wise Development/pgadmin/username }}"
