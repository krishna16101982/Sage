#!/bin/sh
# Runs when the User Panel container starts: writes app-settings.json from API_BASE_URL.
# "/" (default) = the API is on the same address as the panel (nginx forwards /api).
set -e
printf '{ "apiBaseUrl": "%s" }\n' "${API_BASE_URL:-/}" > /usr/share/nginx/html/app-settings.json
