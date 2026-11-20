/** ng build --configuration=production. Set apiBaseUrl to the production User Panel API, e.g. 'https://sales-api.example.com'. */
export const environment = {
  name: 'prod',
  production: true,
  // User API on the server (IIS site, port 8083). The User API must allow this panel's address in Cors:AllowedOrigins.
  apiBaseUrl: 'http://localhost:8083',
};
