# Build context: frontend/, filtered by frontend/.dockerignore.
#   docker build -t helpdesk-web frontend

FROM node:24.21.0-alpine3.24 AS build
WORKDIR /app
# Corepack provides the pnpm version pinned in package.json ("packageManager").
RUN corepack enable

# Manifest and lockfile first, so the install layer is reused until a dependency changes.
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm build

FROM nginxinc/nginx-unprivileged:1.30.5-alpine3.24 AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
# Non-root user of the base image; it listens on 8080 because it cannot bind ports below 1024.
USER 101
EXPOSE 8080
