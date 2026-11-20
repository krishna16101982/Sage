/**
 * ng build --configuration=production.
 * The Admin API is on port 8084 of the same machine the panel is opened from: http://localhost:8082 calls
 * http://localhost:8084, http://103.205.65.143:8082 calls http://103.205.65.143:8084. So the same build works by
 * localhost, by IP or by server name. The Admin API must allow the panel's address in Cors:AllowedOrigins.
 * For a real domain (e.g. https://admin-api.example.com) put that address here instead.
 */
const API_PORT = 8084;

export const environment = {
  name: 'prod',
  production: true,
  apiBaseUrl: typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${API_PORT}` : `http://localhost:${API_PORT}`,
};
