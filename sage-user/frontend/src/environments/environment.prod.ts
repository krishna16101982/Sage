/**
 * ng build --configuration=production.
 * The User API is on port 8083 of the same machine the panel is opened from: http://localhost:8081 calls
 * http://localhost:8083, http://103.205.65.143:8081 calls http://103.205.65.143:8083. So the same build works by
 * localhost, by IP or by server name. The User API must allow the panel's address in Cors:AllowedOrigins.
 * For a real domain (e.g. https://sales-api.example.com) put that address here instead.
 */
const API_PORT = 8083;

export const environment = {
  name: 'prod',
  production: true,
  apiBaseUrl: typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:${API_PORT}` : `http://localhost:${API_PORT}`,
};
