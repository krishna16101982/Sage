/**
 * STAGING.
 * Build: ng build --configuration=qat
 * apiBaseUrl: the staging Admin API. Same host as the panel on port 8084 unless set to a full address
 * (e.g. 'https://staging-admin-api.example.com').
 * app-settings.json (next to index.html on the server) can override apiBaseUrl without a rebuild.
 * The Admin API must allow the panel's address in Cors:AllowedOrigins.
 */
/** The API on the same machine the panel is opened from (localhost, IP or server name), port 8084. */
const sameHost = (port: number) =>
  typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${port}` : `http://localhost:${port}`;

export const environment = {
  name: 'qat',
  production: true,
  apiBaseUrl: sameHost(8084),
};
