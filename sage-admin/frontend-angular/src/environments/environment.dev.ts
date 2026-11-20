/**
 * SERVER, opened on localhost (e.g. http://localhost:8082).
 * Build: ng build --configuration=dev
 * apiBaseUrl: the Admin API on the server, port 8084 (http://localhost:8084).
 * app-settings.json (next to index.html on the server) can override apiBaseUrl without a rebuild.
 * The Admin API must allow the panel's address in Cors:AllowedOrigins.
 */
/** The API on the same machine the panel is opened from (localhost, IP or server name), port 8084. */
const sameHost = (port: number) =>
  typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${port}` : `http://localhost:${port}`;

export const environment = {
  name: 'dev',
  production: true,
  apiBaseUrl: sameHost(8084),
};
