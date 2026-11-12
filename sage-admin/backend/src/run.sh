#!/bin/bash
API_BINARY_NAME=${1}
API_PORT_NUMBER=${2}

# Default ENTRYPOINT script to start the API process (same convention as the ixcheck APIs).
# Secrets and environment come from container environment variables, e.g.
#   DatabaseSettings__ConnectionString, JwtIssuerOptions__SecurityKey, ASPNETCORE_ENVIRONMENT

exec dotnet ./${API_BINARY_NAME}.dll --port=${API_PORT_NUMBER}
