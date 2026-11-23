/**
 * LIVE: the panel on its domain, http(s)://userpanel.new.ocpwebserver.com
 * Build: ng build --configuration=production
 * apiBaseUrl: the User API on its own domain, userapi.new.ocpwebserver.com, with the same http / https as the
 * page (so it keeps working when the sites move to https).
 * app-settings.json (next to index.html on the server) can override apiBaseUrl without a rebuild.
 * The User API must allow the panel's address in Cors:AllowedOrigins.
 */
const API_HOST = 'userapi.new.ocpwebserver.com';

export const environment = {
  name: 'prod',
  production: true,
  apiBaseUrl: typeof window !== 'undefined' ? `${window.location.protocol}//${API_HOST}` : `https://${API_HOST}`,
};
