FROM node:24-bookworm-slim AS dependencies
WORKDIR /app
COPY whatsapp-gateway/package.json whatsapp-gateway/package-lock.json ./
ENV PUPPETEER_SKIP_DOWNLOAD=true
RUN npm ci --omit=dev

FROM node:24-bookworm-slim AS runtime
ENV NODE_ENV=production \
    HOME=/tmp \
    PORT=3000 \
    HOST=0.0.0.0 \
    DETARA_WHATSAPP_SESSIONS_PATH=/app/sessions \
    PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
RUN apt-get update \
    && apt-get install -y --no-install-recommends chromium ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=dependencies /app/node_modules ./node_modules
COPY whatsapp-gateway/package.json ./package.json
COPY whatsapp-gateway/src ./src
RUN mkdir -p /app/sessions && chown -R node:node /app
USER node
EXPOSE 3000
CMD ["node", "src/server.js"]
