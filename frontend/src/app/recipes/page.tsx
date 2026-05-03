"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { getValidToken, setRedirectPath } from "@/lib/auth";

type Recipe = {
  id: number;
  title: string;
  description: string | null;
  imageUrl: string | null;
  preparationTimeMinutes: number;
  cookingTimeMinutes: number;
  servings: number;
  categoryId: number;
  categoryName: string | null;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function RecipesPage() {
  const router = useRouter();
  const pathname = usePathname();
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    async function fetchRecipes() {
      try {
        const response = await fetch(`${API_URL}/api/recipe`);
        const data = (await response.json()) as ApiResponse<Recipe[]>;

        if (!data.isSuccess || !response.ok) {
          throw new Error(data.errorMessages?.[0] ?? "Failed to load recipes.");
        }

        setRecipes(data.result);
      } catch (error) {
        setErrorMessage(
          error instanceof Error ? error.message : "Failed to load recipes."
        );
      } finally {
        setIsLoading(false);
      }
    }

    fetchRecipes();
  }, []);

  async function handleFavorite(recipeId: number) {
    setMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        setRedirectPath(pathname);
        router.push("/login");
        return;
      }

      const response = await fetch(
        `${API_URL}/api/favoriterecipe/${recipeId}`,
        {
          method: "POST",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe could not be added to favorites.");
      }

      setMessage("Recipe added to favorites.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Recipe could not be added to favorites."
      );
    }
  }

  return (
    <section className="page-section">
      <div className="flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
        <div className="page-heading">
          <p className="eyebrow">Recipes</p>
          <h1 className="page-title">Browse meal ideas</h1>
          <p className="page-description">
            Recipes are loaded from the backend API and displayed in the same
            clean card layout.
          </p>
        </div>

        <div className="w-full max-w-sm">
          <label className="form-label" htmlFor="search">
            Search recipes
          </label>
          <input
            id="search"
            type="search"
            placeholder="Search by name"
            className="form-input"
          />
        </div>
      </div>

      {isLoading && (
        <RecipesLoadingState />
      )}

      {errorMessage && (
        <div className="error-panel">
          {errorMessage}
        </div>
      )}

      {message && (
        <div className="status-panel">
          {message}
        </div>
      )}

      {!isLoading && !errorMessage && recipes.length === 0 && (
        <div className="flex min-h-[35vh] items-center justify-center rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center">
          <div className="max-w-md">
            <h2 className="text-xl font-semibold text-slate-900">
              No recipes yet. Add your first recipe.
            </h2>
            <p className="mt-3 text-sm leading-6 text-slate-600">
              Recipes created from the Admin dashboard will appear here for everyone to browse.
            </p>
            <Link href="/admin" className="primary-button mt-6 inline-flex">
              Go to Admin Dashboard
            </Link>
          </div>
        </div>
      )}

      {!isLoading && !errorMessage && recipes.length > 0 && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {recipes.map((recipe) => (
            <RecipeCard
              key={recipe.id}
              recipe={recipe}
              onFavorite={handleFavorite}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function RecipesLoadingState() {
  return (
    <div className="flex min-h-[45vh] items-center justify-center">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div className="mx-auto h-10 w-10 animate-spin rounded-full border-4 border-emerald-100 border-t-emerald-600" />
        <p className="mt-4 text-center text-sm font-medium text-slate-700">
          Loading recipes...
        </p>
        <div className="mt-6 space-y-3">
          <div className="h-4 animate-pulse rounded bg-slate-100" />
          <div className="h-4 w-5/6 animate-pulse rounded bg-slate-100" />
          <div className="h-4 w-2/3 animate-pulse rounded bg-slate-100" />
        </div>
      </div>
    </div>
  );
}

function RecipeCard({
  recipe,
  onFavorite
}: {
  recipe: Recipe;
  onFavorite: (recipeId: number) => void;
}) {
  const hasValidImageUrl =
    recipe.imageUrl !== null
    && recipe.imageUrl.trim().length > 0
    && recipe.imageUrl !== "string"
    && URL.canParse(recipe.imageUrl);

  return (
    <article className="app-card interactive-card overflow-hidden">
      {hasValidImageUrl ? (
        <div
          className="h-40 bg-cover bg-center"
          style={{ backgroundImage: `url(${recipe.imageUrl})` }}
          aria-label={recipe.title}
        />
      ) : (
        <div className="flex h-40 items-center justify-center bg-emerald-50 text-sm font-medium text-emerald-700">
          Image placeholder
        </div>
      )}

      <div className="space-y-4 p-4">
        <div>
          <h2 className="text-lg font-semibold tracking-normal">
            {recipe.title}
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600">
            {recipe.description ?? "No description provided."}
          </p>
        </div>

        <div className="grid grid-cols-2 gap-3 border-t border-slate-100 pt-4 text-sm text-slate-600">
          <div>
            <span className="block text-xs font-semibold uppercase tracking-wide text-slate-500">
              Servings
            </span>
            <span>{recipe.servings}</span>
          </div>
          <div>
            <span className="block text-xs font-semibold uppercase tracking-wide text-slate-500">
              Prep time
            </span>
            <span>{recipe.preparationTimeMinutes} min</span>
          </div>
        </div>

        <Link
          href={`/recipes/${recipe.id}`}
          className="primary-button block w-full text-center"
        >
          View Details
        </Link>
        <button
          type="button"
          onClick={() => onFavorite(recipe.id)}
          className="secondary-button w-full"
        >
          Favorite
        </button>
      </div>
    </article>
  );
}
