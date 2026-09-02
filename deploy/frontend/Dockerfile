FROM node:24-alpine AS build
WORKDIR /source
COPY src/CivicFlow.Client/package.json src/CivicFlow.Client/package-lock.json ./
RUN npm ci
COPY src/CivicFlow.Client/ ./
ARG VITE_MAP_TILE_URL
ARG VITE_MAP_ATTRIBUTION
ENV VITE_API_URL=/api
ENV VITE_MAP_TILE_URL=$VITE_MAP_TILE_URL
ENV VITE_MAP_ATTRIBUTION=$VITE_MAP_ATTRIBUTION
RUN npm run build

FROM caddy:2.10-alpine
COPY deploy/frontend/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /source/dist /srv
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=5 \
    CMD wget -q -O /dev/null "http://127.0.0.1:${PORT:-8080}/healthz" || exit 1
