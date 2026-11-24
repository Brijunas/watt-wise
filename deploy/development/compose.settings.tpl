# Template for deploy/.env, the non-secret settings of the Development compose stack.
# Docker Compose reads deploy/.env automatically, so everyday compose commands need no
# op run. ./setup renders it (and renders it again, only when it differs, whenever one of
# these 1Password values changes). The command it runs:
#
#   op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env
#
# Passwords are never rendered; they stay in compose.env (op run).
# GRAFANA_URL is not used by compose: the grafana MCP server reads it (.claude/mcp/grafana.sh).
# API_URL, JOBS_URL, FRONTEND_URL and ADMIN_URL are the four apps' browser URLs. ./setup derives
# each app's published <APP>_BIND_ADDRESS and <APP>_PORT from them and appends those eight keys
# to deploy/.env (docs in deploy/docs/development-stack.md); they are not in this template.
# Vault: "Watt Wise Development", items postgres-admin, grafana, otlp, mailpit-smtp, mailpit,
# api, jobs, frontend and admin.
POSTGRES_BIND_ADDRESS="{{ op://Watt Wise Development/postgres-admin/server }}"
POSTGRES_PORT="{{ op://Watt Wise Development/postgres-admin/port }}"
POSTGRES_USER="{{ op://Watt Wise Development/postgres-admin/username }}"
POSTGRES_DB="{{ op://Watt Wise Development/postgres-admin/database }}"
GRAFANA_BIND_ADDRESS="{{ op://Watt Wise Development/grafana/server }}"
GRAFANA_PORT="{{ op://Watt Wise Development/grafana/port }}"
GRAFANA_URL="{{ op://Watt Wise Development/grafana/url }}"
OTLP_BIND_ADDRESS="{{ op://Watt Wise Development/otlp/server }}"
OTLP_PORT="{{ op://Watt Wise Development/otlp/port }}"
MAILPIT_SMTP_BIND_ADDRESS="{{ op://Watt Wise Development/mailpit-smtp/server }}"
MAILPIT_SMTP_PORT="{{ op://Watt Wise Development/mailpit-smtp/port }}"
MAILPIT_BIND_ADDRESS="{{ op://Watt Wise Development/mailpit/server }}"
MAILPIT_PORT="{{ op://Watt Wise Development/mailpit/port }}"
API_URL="{{ op://Watt Wise Development/api/url }}"
JOBS_URL="{{ op://Watt Wise Development/jobs/url }}"
FRONTEND_URL="{{ op://Watt Wise Development/frontend/url }}"
ADMIN_URL="{{ op://Watt Wise Development/admin/url }}"
