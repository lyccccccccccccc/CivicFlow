# Security policy

## Supported scope

Security fixes are applied to the current `master` branch. This portfolio project is not a certified production government service.

## Reporting a vulnerability

Do not open a public issue containing an exploit, credential, personal information, or private attachment. Use GitHub private vulnerability reporting when it is enabled. Until then, contact the repository owner through their public GitHub profile without sensitive evidence in the first message.

Include the affected endpoint/component, impact, minimal reproduction, and suggested remediation. Remove tokens, connection strings, personal information, and private Blob addresses from logs and screenshots.

## Deployment requirements

- Supply database, JWT, and storage configuration through environment variables, .NET User Secrets, managed identity, or a deployment secret store.
- Never deploy with local demo accounts or development passwords enabled.
- Keep the Blob container private and authorize every attachment operation through the API.
- Apply reviewed EF migration bundles before production startup.
- Add malware scanning and quarantine before accepting production uploads.
- Review the configured map tile provider's production usage policy.
- Use HTTPS, restricted CORS origins, rate limiting, centralized audit retention, monitoring, and tested backup recovery.
