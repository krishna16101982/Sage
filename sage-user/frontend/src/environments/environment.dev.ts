/**
 * SERVER, opened on localhost (e.g. http://localhost:8081).
 * Build: ng build --configuration=dev
 * apiBaseUrl: the User API on the server, port 8083 (http://localhost:8083).
 * app-settings.json (next to index.html on the server) can override apiBaseUrl without a rebuild.
 * The User API must allow the panel's address in Cors:AllowedOrigins.
 */
/** The API on the same machine the panel is opened from (localhost, IP or server name), port 8083. */
const sameHost = (port: number) =>
  typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${port}` : `http://localhost:${port}`;

export const environment = {
  name: 'dev',
  production: true,
  apiBaseUrl: sameHost(8083),
};
