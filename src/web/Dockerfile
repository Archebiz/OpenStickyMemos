# Stage 1: Build
FROM node:22.23.1-alpine3.24 AS build
WORKDIR /app
COPY open-sticky-memos/package*.json ./
RUN npm ci
COPY open-sticky-memos/ .
RUN npm run build --prod

# Stage 2: Serve with Nginx (stable atualizado)
FROM nginx:1.30.3-alpine3.23-slim
COPY --from=build /app/dist/open-sticky-memos/browser /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

RUN chmod +x /docker-entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]
