# MealApp Project Progress

## Day 1
- Created ASP.NET Core Web API project with controllers.
- Added EF Core SQL Server and `ApplicationDbContext`.

## Day 2
- Added ASP.NET Core Identity with `ApplicationUser`.
- Implemented register and login endpoints.
- Added JWT authentication and protected test endpoint.
- Added role setup, role seeding, role assignment during registration, and role claims in JWT.

## Day 3
- Added CRUD APIs for categories, ingredients, units, and recipes.
- Added recipe ingredients, steps, tools, and complete recipe details endpoint.
- Fixed JSON cycle issues by returning response DTOs instead of EF entities.

## Day 4
- Added recipe servings preview endpoint.
- Added meal plan feature for authenticated users.
- Added shopping list generation from the authenticated user's meal plans.
- Shopping list generation scales recipe ingredient quantities by planned servings and merges matching ingredient/unit pairs.
- Added shopping list management endpoints for viewing owned lists, viewing one owned list, toggling items, updating item quantity/unit, and deleting items.
- Applied final authorization rules: public read endpoints, Admin-only content management endpoints, and logged-in-only meal plan/shopping list endpoints.
- Added standard `ApiResponse` format and global exception/status-code handling for consistent success and error responses.
- Completed full backend smoke test covering authentication, authorization, admin CRUD, recipe composition, servings preview, meal plans, and shopping list management.
- Fixed MVC action discovery issue by marking `ApiControllerBase` response helper methods as non-actions.
