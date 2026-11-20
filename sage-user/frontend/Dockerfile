# Sage User Panel image: the Angular build served by nginx, which also forwards /api to the API container.
# Build context: this folder.
#   docker build --build-arg BUILD_CONFIGURATION=qat -t sage-user-panel .
# Run-time settings (environment variables):
#   API_UPSTREAM  where nginx forwards /api (default http://user-api:8080, the API container in docker-compose.yml)
#   API_BASE_URL  written to app-settings.json; "/" (default) = the API is on the same address as the panel
FROM node:22-alpine AS build
ARG BUILD_CONFIGURATION=qat
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN NODE_OPTIONS=--max-old-space-size=2048 npx ng build --configuration ${BUILD_CONFIGURATION}

FROM nginx:1.27-alpine
COPY docker/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY docker/40-app-settings.sh /docker-entrypoint.d/40-app-settings.sh
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-app-settings.sh && chmod +x /docker-entrypoint.d/40-app-settings.sh
COPY --from=build /app/dist/sage-user-angular/browser /usr/share/nginx/html
ENV API_UPSTREAM=http://user-api:8080 \
    API_BASE_URL=/
EXPOSE 80
