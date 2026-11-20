/**
 * PUBLIC: the server opened by its public address (now http://103.205.65.143:8081, later a domain).
 * Build: ng build --configuration=production
 * apiBaseUrl: the User API on the same address, port 8083 (http://103.205.65.143:8083).
 * When a domain is ready, put its API address here, e.g. 'https://sales-api.example.com'.
 * app-settings.json (next to index.html on the server) can override apiBaseUrl without a rebuild.
 * The User API must allow the panel's address in Cors:AllowedOrigins.
 */
/** The API on the same machine the panel is opened from (localhost, IP or server name), port 8083. */
const sameHost = (port: number) =>
  typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${port}` : `http://localhost:${port}`;

export const environment = {
  name: 'prod',
  production: true,
  apiBaseUrl: sameHost(8083),
};
