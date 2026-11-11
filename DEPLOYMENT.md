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
```

Notes:

- Do not use LocalDB in production.
- Do not commit real connection strings, JWT secrets, or admin passwords.
- Add more allowed frontend origins with `Cors__AllowedOrigins__1`, `Cors__AllowedOrigins__2`, and so on.
- Set `SeedSampleData=true` only if you intentionally want demo recipes seeded in production.

## Support Availability

Outgoing support email delivery has been removed. No email-provider environment
variables, credentials, or enable flags are required. Old email-provider settings
are unused and can be removed from the hosting platform separately.

The Support page and `POST /api/support` route remain. Valid submissions receive
HTTP 503 with `Support email delivery is temporarily unavailable. Please try again later.`
Requests and attachments are not sent, queued, or stored. Existing validation and
authenticated account checks still apply. All required backend settings listed
above remain unchanged. Database connectivity errors must be diagnosed separately.

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
