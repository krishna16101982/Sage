import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';

/**
 * app-settings.json (next to index.html) is read when the panel opens, so the User API address can be changed on the
 * server without a rebuild. "apiBaseUrl": "" keeps the build's address (qat: the same host as the page, port 8083;
 * production: userapi.new.ocpwebserver.com); a full address such as "http://103.205.65.143:8083" or "https://api.example.com" is used as it is;
 * "/" means the same address as the page (the Docker image, where nginx forwards /api to the API).
 */
async function loadSettings(): Promise<void> {
  try {
    const res = await fetch('app-settings.json', { cache: 'no-store' });
    if (!res.ok) return;
    const settings = await res.json();
    const url = typeof settings?.apiBaseUrl === 'string' ? settings.apiBaseUrl.trim() : '';
    if (url) environment.apiBaseUrl = url === '/' ? '' : url.replace(/\/+$/, '');
  } catch {
    // No file, or not valid JSON: keep the build's address.
  }
}

loadSettings()
  .then(() => bootstrapApplication(App, appConfig))
  .catch((err) => console.error(err));
