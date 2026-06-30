# Stage 1: Build
FROM node:22.23.1-alpine3.24 AS build
WORKDIR /app
COPY open-sticky-memos/package*.json ./
RUN npm ci
COPY open-sticky-memos/ .
RUN npm run build --prod

# Stage 2: Serve with Nginx (stable atualizado)
FROM nginx:1.30.3-alpine3.23-slim AS runtime

# Copy built Angular app
COPY --from=build /app/dist/open-sticky-memos/browser /usr/share/nginx/html

# Entrypoint: generate config.json from API_URL env var, then start nginx
RUN printf '#!/bin/sh\nAPI="${API_URL:-http://localhost:5000}"\ncat > /usr/share/nginx/html/assets/config.json <<EOF\n{"apiUrl":"${API}","signalrUrl":"${API}/hubs/notes"}\nEOF\nexec nginx -g "daemon off;"\n' > /docker-entrypoint.sh && chmod +x /docker-entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]

RUN chmod +x /docker-entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]
