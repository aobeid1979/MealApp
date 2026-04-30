# MealApp

MealApp has an ASP.NET Core Web API backend and a Next.js frontend.

## Local Secret Setup

Sensitive backend values should not be committed in `appsettings.json`.
For local development, use .NET User Secrets from the backend project folder.

```powershell
cd backend
dotnet user-secrets set "Jwt:Secret" "replace-with-a-long-random-secret-key"
dotnet user-secrets set "AdminUser:Password" "replace-with-a-strong-admin-password"
```

The default seeded admin account is:

```text
Email: amfo250@gmail.com
Name: Ammar
Role: Admin
```

The admin user is created only when `AdminUser:Password` is configured.
If the admin user already exists, the app will not create a duplicate.

## Environment Variable Alternative

You can also set secrets with environment variables.

PowerShell:

```powershell
$env:Jwt__Secret = "replace-with-a-long-random-secret-key"
$env:AdminUser__Password = "replace-with-a-strong-admin-password"
```

Windows Command Prompt:

```cmd
set Jwt__Secret=replace-with-a-long-random-secret-key
set AdminUser__Password=replace-with-a-strong-admin-password
```

## Frontend API URL

The frontend reads the API base URL from `frontend/.env.local`.

```env
NEXT_PUBLIC_API_URL=http://localhost:5280
```

Restart the frontend after changing `.env.local`.
