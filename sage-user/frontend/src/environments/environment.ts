/**
 * Local development (`ng serve`). Other environments replace this file at build time (angular.json → fileReplacements):
 *   environment.dev.ts  → ng build --configuration=dev
 *   environment.qat.ts  → ng build --configuration=qat
 *   environment.prod.ts → ng build --configuration=production
 *
 * apiBaseUrl: the Sage User Panel API. Empty string = same origin (ng serve forwards /api to http://localhost:5090 via
 * proxy.conf.json, no CORS needed). A full URL calls the API directly; that site must be in the API's Cors:AllowedOrigins.
 */
export const environment = {
  name: 'local',
  production: false,
  apiBaseUrl: '',
};
