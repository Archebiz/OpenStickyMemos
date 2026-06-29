# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app
COPY open-sticky-memos/package*.json ./
RUN npm ci
COPY open-sticky-memos/ .
RUN npm run build --prod

# Stage 2: Serve with Nginx
FROM nginx:alpine
COPY --from=build /app/dist/open-sticky-memos/browser /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
