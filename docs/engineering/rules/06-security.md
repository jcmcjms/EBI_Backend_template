# Security Rules

Never commit passwords, tokens, signing keys, API keys, credentials, private certificates, or production secrets.

Never trust client-supplied roles, ownership IDs, branch IDs, or tenant IDs without server-side validation.

Never log passwords, access tokens, refresh tokens, API keys, connection strings, or unnecessary sensitive/financial/customer data.

Use environment variables, user secrets, or managed secret stores for secrets.

Use explicit CORS origins. Do not combine `AllowAnyOrigin` with credentials.

Validate uploaded files for size/type/name/storage isolation and apply malware scanning where required.

Use parameterized database APIs. Never interpolate untrusted input into SQL.

Internal exceptions must not leak to clients.
