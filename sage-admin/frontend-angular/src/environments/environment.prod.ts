/** ng build --configuration=production. Set apiBaseUrl to the production Admin API, e.g. 'https://admin-api.example.com'. */
export const environment = {
  name: 'prod',
  production: true,
  // Admin API on the server (IIS site, port 8084). The Admin API must allow this panel's address in Cors:AllowedOrigins.
  apiBaseUrl: 'http://localhost:8084',
};
