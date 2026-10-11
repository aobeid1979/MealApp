# MealApp Deployment Guide

MealApp has two deployable parts:

- Backend: ASP.NET Core Web API in `backend`
- Frontend: Next.js app in `frontend`

## Required Backend Environment Variables

Set these on the backend hosting platform:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=your-production-sql-server-connection-string
Jwt__Secret=replace-with-a-long-random-secret-key
Jwt__Issuer=MealApp
Jwt__Audience=MealAppUsers
AdminUser__Email=admin@example.com
AdminUser__Password=replace-with-a-strong-admin-password
Cors__AllowedOrigins__0=https://your-frontend-domain.com
SeedSampleData=false
BrevoEmail__ApiKey=replace-with-your-private-brevo-api-key
BrevoEmail__SenderEmail=verified-sender@example.com
BrevoEmail__SenderName=your-support-sender-name
BrevoEmail__ToEmail=your-support-recipient@example.com
```

Notes:

- Do not use LocalDB in production.
- Do not commit real connection strings, JWT secrets, or admin passwords.
- Add more allowed frontend origins with `Cors__AllowedOrigins__1`, `Cors__AllowedOrigins__2`, and so on.
- Set `SeedSampleData=true` only if you intentionally want demo recipes seeded in production.
- All four Brevo variables are required by production startup validation. Configure
  them before restarting the service, or startup will fail clearly.

## Support Email Configuration

Support sends email through Brevo's transactional HTTP API over HTTPS; no SMTP
credentials or outbound SMTP ports are used. Configuration keys map as follows:

| Configuration key | Hosting environment variable |
| --- | --- |
| `BrevoEmail:ApiKey` | `BrevoEmail__ApiKey` |
| `BrevoEmail:SenderEmail` | `BrevoEmail__SenderEmail` |
| `BrevoEmail:SenderName` | `BrevoEmail__SenderName` |
| `BrevoEmail:ToEmail` | `BrevoEmail__ToEmail` |

Use a private transactional API key stored in the hosting platform's secret
settings. Verify the sender email or its domain in Brevo. Set the intended support
recipient on the host. All example values here are placeholders, not credentials.
For local development use .NET User Secrets as described in README.md.

## Historical Signing Key Check

Commit `72631a1` contained a signing-secret literal under `Jwt:Secret` in
`backend/appsettings.json`. Current configuration does not contain that value;
production must supply `Jwt__Secret` externally. Its external value cannot be
verified from this repository. Treat the historical key as exposed and confirm
that no active environment reuses it. If it is reused, arrange an explicitly
approved credential rotation; existing tokens signed with the old key will stop
working. Removing a value from current files does not remove it from Git history.

## Required Frontend Environment Variables

Set this on the frontend hosting platform:

```text
NEXT_PUBLIC_API_URL=https://your-backend-domain.com
```

For local development, copy:

```text
frontend/.env.local.example
```

to:

```text
frontend/.env.local
```

## Backend Deployment Steps

1. Provision a production SQL Server database.
2. Configure all required backend environment variables on the host.
3. Deploy the `backend` project.
4. Ensure the backend runs with:

```text
ASPNETCORE_ENVIRONMENT=Production
```

5. Apply EF Core migrations to the production database.
6. Start or restart the backend service.
7. Confirm the API responds successfully.

## Frontend Deployment Steps

1. Configure `NEXT_PUBLIC_API_URL` with the deployed backend URL.
2. Deploy the `frontend` project.
3. Build the frontend with:

```bash
npm run build
```

4. Confirm the deployed frontend can call the backend without CORS errors.

## Migration Instructions

Run migrations from the `backend` folder after production environment variables are configured:

```powershell
cd backend
dotnet ef database update
```

For production, make sure `ConnectionStrings__DefaultConnection` points to the production SQL Server database before running the command.

If your hosting provider runs deployment commands, configure the migration command there or run it manually from a trusted machine that has access to the production database.

## Local Verification Commands

Backend:

```powershell
cd backend
dotnet build .\MealApp.csproj --no-restore /p:UseAppHost=false
dotnet test .\MealApp.Tests\MealApp.Tests.csproj --no-restore /p:UseAppHost=false
```

Frontend:

```powershell
cd frontend
npm run lint
node --test tests/agent-response.test.cjs
npm run build
```

## Production Safety Checklist

- `appsettings.Production.json` contains no secrets.
- Backend CORS includes the deployed frontend URL.
- Frontend `NEXT_PUBLIC_API_URL` points to the deployed backend URL.
- `Jwt__Secret` is long, random, and private.
- `AdminUser__Password` is strong and private.
- Sample data is disabled unless intentionally enabled.
- Test/debug endpoints are not included in Release builds.
