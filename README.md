# MealApp

MealApp has an ASP.NET Core Web API backend and a Next.js frontend.

## Local Secret Setup

Sensitive backend values should not be committed in `appsettings.json`.
For local development, use .NET User Secrets from the backend project folder.

```powershell
cd backend
dotnet user-secrets set "Jwt:Secret" "replace-with-a-long-random-secret-key"
dotnet user-secrets set "AdminUser:Email" "admin@example.com"
dotnet user-secrets set "AdminUser:Password" "replace-with-a-strong-admin-password"
```

The default seeded admin account uses the email configured in `AdminUser:Email`:

```text
Name: Ammar
Role: Admin
```

The admin user is created only when both `AdminUser:Email` and `AdminUser:Password` are configured.
If the admin user already exists, the app will not create a duplicate.

## Environment Variable Alternative

You can also set secrets with environment variables.

PowerShell:

```powershell
$env:Jwt__Secret = "replace-with-a-long-random-secret-key"
$env:AdminUser__Email = "admin@example.com"
$env:AdminUser__Password = "replace-with-a-strong-admin-password"
$env:Cors__AllowedOrigins__0 = "https://your-frontend-domain.com"
```

Windows Command Prompt:

```cmd
set Jwt__Secret=replace-with-a-long-random-secret-key
set AdminUser__Email=admin@example.com
set AdminUser__Password=replace-with-a-strong-admin-password
set Cors__AllowedOrigins__0=https://your-frontend-domain.com
```

## Backend CORS

Allowed frontend URLs are read from `Cors:AllowedOrigins`.
Local development keeps `http://localhost:3000` in `backend/appsettings.Development.json`.
For production, configure the deployed frontend URL with environment variables such as:

```text
Cors__AllowedOrigins__0=https://your-frontend-domain.com
```

Add more origins with `Cors__AllowedOrigins__1`, `Cors__AllowedOrigins__2`, and so on.

## Production Backend Configuration

`backend/appsettings.Production.json` contains no LocalDB connection string and no secrets.
Set production values on the hosting platform with environment variables:

```text
ConnectionStrings__DefaultConnection=your-production-sql-server-connection-string
Jwt__Secret=replace-with-a-long-random-secret-key
Jwt__Issuer=MealApp
Jwt__Audience=MealAppUsers
AdminUser__Email=admin@example.com
AdminUser__Password=replace-with-a-strong-admin-password
Cors__AllowedOrigins__0=https://your-frontend-domain.com
SeedSampleData=false
```

ASP.NET Core loads `appsettings.Production.json` when `ASPNETCORE_ENVIRONMENT=Production`,
then environment variables override those placeholder values.

Sample recipe data is enabled by default only in local Development.
In Production, set `SeedSampleData=true` only if you intentionally want demo data seeded.

## Frontend API URL

The frontend reads the API base URL from `frontend/.env.local`.
Copy `frontend/.env.local.example` to `frontend/.env.local` for local development.

```env
NEXT_PUBLIC_API_URL=http://localhost:5280
```

Restart the frontend after changing `.env.local`.
For production, set `NEXT_PUBLIC_API_URL` to the deployed backend URL.
