# Sage User API image (used by docker-compose.yml and GitHub Actions).
# Build context: the REPOSITORY ROOT (the API needs sage-admin/sage-common-lib):
#   docker build -f sage-user/backend/Dockerfile -t sage-user-api .
# Settings come from environment variables at run time (never baked in):
#   DatabaseSettings__ConnectionString, JwtIssuerOptions__SecurityKey, AuthSettings__FrontendBaseUrl, Email__*
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached while the project files do not change).
COPY sage-admin/sage-common-lib/src/SageCommonLib.csproj sage-admin/sage-common-lib/src/
COPY sage-user/backend/src/SageUserApi.csproj sage-user/backend/src/
RUN dotnet restore sage-user/backend/src/SageUserApi.csproj

COPY sage-admin/sage-common-lib/src/ sage-admin/sage-common-lib/src/
COPY sage-user/backend/src/ sage-user/backend/src/
RUN dotnet publish sage-user/backend/src/SageUserApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# DejaVu fonts for the order form PDF (has the rupee sign; the PDF code uses it on Linux).
RUN apt-get update \
 && apt-get install -y --no-install-recommends fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
# Logs and order-form PDFs live in volumes; the folders belong to the non-root "app" user.
RUN mkdir -p /app/logs /app/storage/order-forms && chown -R "$APP_UID" /app/logs /app/storage
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Production \
    Serilog__FilePath=/app/logs \
    OrderFormPdf__StoragePath=/app/storage/order-forms
EXPOSE 8080
ENTRYPOINT ["dotnet", "SageUserApi.dll", "--port=8080"]
