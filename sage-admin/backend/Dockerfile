# Sage Admin API image (used by docker-compose.yml and GitHub Actions).
# Build context: the REPOSITORY ROOT (the API needs sage-admin/sage-common-lib):
#   docker build -f sage-admin/backend/Dockerfile --build-arg GIT_BUILD_TAG=<tag> -t sage-admin-api .
# Settings come from environment variables at run time (never baked in):
#   DatabaseSettings__ConnectionString, JwtIssuerOptions__SecurityKey, AuthSettings__FrontendBaseUrl, Email__*
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG GIT_BUILD_TAG=local
WORKDIR /src

# Restore first (cached while the project files do not change).
COPY sage-admin/sage-common-lib/src/SageCommonLib.csproj sage-admin/sage-common-lib/src/
COPY sage-admin/backend/src/SageAdminApi.csproj sage-admin/backend/src/
RUN dotnet restore sage-admin/backend/src/SageAdminApi.csproj

COPY sage-admin/sage-common-lib/src/ sage-admin/sage-common-lib/src/
COPY sage-admin/backend/src/ sage-admin/backend/src/
RUN sed -i "s/GIT_PROJECT_NAME/sage-admin-api/g; s/GIT_BUILD_TAG/${GIT_BUILD_TAG}/g" sage-admin/backend/src/Controllers/StatusController.cs \
 && dotnet publish sage-admin/backend/src/SageAdminApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
# Logs and order-form PDFs live in volumes; the folders belong to the non-root "app" user.
RUN mkdir -p /app/logs /app/storage/order-forms && chown -R "$APP_UID" /app/logs /app/storage
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Production \
    Serilog__FilePath=/app/logs \
    OrderFormPdf__StoragePath=/app/storage/order-forms
EXPOSE 8080
ENTRYPOINT ["dotnet", "SageAdminApi.dll", "--port=8080"]
