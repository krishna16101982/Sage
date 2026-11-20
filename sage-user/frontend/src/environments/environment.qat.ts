/**
 * STAGING.
 * Build: ng build --configuration=qat
 * apiBaseUrl: the staging User API. Same host as the panel on port 8083 unless set to a full address
 * (e.g. 'https://staging-sales-api.example.com').
 * app-settings.json (next to index.html on the server) can override apiBaseUrl without a rebuild.
 * The User API must allow the panel's address in Cors:AllowedOrigins.
 */
/** The API on the same machine the panel is opened from (localhost, IP or server name), port 8083. */
const sameHost = (port: number) =>
  typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${port}` : `http://localhost:${port}`;

export const environment = {
  name: 'qat',
  production: true,
  apiBaseUrl: sameHost(8083),
};
