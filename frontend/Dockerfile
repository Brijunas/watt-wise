# Build from the repo root: docker build -f frontend/Dockerfile [--target dev] -t wattwise-frontend .
# Stages, base images and the nginx rules: deploy/docs/images.md.

# deps: Node and pnpm, and the packages of the whole workspace fetched into the store from the lockfile alone.
# Node 26 ships no corepack, so pnpm comes from npm. Its version follows mise.toml (update-deps).
FROM node:26.13.1-trixie-slim AS deps
RUN npm install -g pnpm@12.18.0 \
    && mkdir /src /pnpm \
    && chown node:node /src /pnpm
ENV HUSKY=0 \
    npm_config_store_dir=/pnpm/store
USER node
WORKDIR /src
COPY --chown=node pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm fetch

# dev: no source copied; Compose bind-mounts the tree at /src.
FROM deps AS dev
ENTRYPOINT ["pnpm", "--filter", "frontend", "dev"]

# build: the sources the build needs, then the same `tsc && vite build` that CI's js job runs.
# --ignore-scripts: the root's preinstall (npx only-allow) needs the network and prepare (husky) needs git.
FROM deps AS build
COPY --chown=node package.json tsconfig.base.json vitest.base.js vitest.setup.js ./
COPY --chown=node frontend/ frontend/
COPY --chown=node packages/ packages/
RUN pnpm install --frozen-lockfile --offline --ignore-scripts --filter frontend...
RUN pnpm --filter frontend build \
    && mkdir out \
    && mv frontend/dist/index.html out/index.html.template \
    && mv frontend/dist out/html

# runtime: the last stage, so a plain docker build gives it.
FROM nginxinc/nginx-unprivileged:1.31.7-trixie AS runtime
USER root
# /etc/nginx is writable by the nginx user in the base image: make it root's, so the running
# process can't change its own configuration. /run/wattwise is where the entry point renders the
# per-container files (/config.js and index.html).
RUN chown -R root:root /etc/nginx \
    && chmod -R go-w /etc/nginx \
    && rm -rf /usr/share/nginx/html/* \
    && install -d -o 101 -g 101 -m 0755 /run/wattwise
COPY --from=build --chown=root:root --chmod=a=rX /src/out/html/ /usr/share/nginx/html/
COPY --from=build --chown=root:root --chmod=a=rX /src/out/index.html.template /etc/nginx/templates/index.html.template
COPY --chown=root:root --chmod=a=rX deploy/nginx/frontend/config.js.template /etc/nginx/templates/config.js.template
COPY --chown=root:root --chmod=a=rX deploy/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --chown=root:root --chmod=a=rx deploy/nginx/05-check-settings.sh /docker-entrypoint.d/05-check-settings.sh
# The guard checks WATTWISE_SETTINGS; envsubst renders only these names and nothing else in the files.
ENV WATTWISE_SETTINGS="API_BASE_URL APP_URL" \
    NGINX_ENVSUBST_OUTPUT_DIR=/run/wattwise \
    NGINX_ENVSUBST_FILTER="^(API_BASE_URL|APP_URL)$"
USER 101
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --start-interval=2s --retries=3 \
    CMD curl -fsS --max-time 2 http://localhost:8080/index.html > /dev/null
