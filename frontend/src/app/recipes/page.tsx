"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { getValidToken, setRedirectPath } from "@/lib/auth";
import { loadFavoriteRecipeIds } from "@/lib/favorites";

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

type Category = {
  id: number;
  name: string;
  description: string | null;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

function getCategoryIdFromUrl() {
  const categoryId = new URLSearchParams(window.location.search).get("categoryId");
  const parsedCategoryId = Number(categoryId);

  return categoryId && Number.isInteger(parsedCategoryId) && parsedCategoryId > 0
    ? parsedCategoryId
    : null;
}

export default function RecipesPage() {
  const router = useRouter();
  const pathname = usePathname();
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [categoryErrorMessage, setCategoryErrorMessage] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [searchText, setSearchText] = useState("");
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [isCategoryFilterReady, setIsCategoryFilterReady] = useState(false);
  const [favoriteRecipeIds, setFavoriteRecipeIds] = useState<Set<number>>(new Set());
  const [pendingFavoriteRecipeId, setPendingFavoriteRecipeId] = useState<number | null>(null);

  useEffect(() => {
    const syncCategoryFilter = window.setTimeout(() => {
      setSelectedCategoryId(getCategoryIdFromUrl());
      setIsCategoryFilterReady(true);
    }, 0);

    return () => window.clearTimeout(syncCategoryFilter);
  }, []);

  useEffect(() => {
    async function fetchCategories() {
      try {
        setCategoryErrorMessage(null);

        const response = await fetch(`${API_URL}/api/category`);
        const data = (await response.json()) as ApiResponse<Category[]>;

        if (!data.isSuccess || !response.ok) {
          throw new Error(data.errorMessages?.[0] ?? "Failed to load categories.");
        }

        setCategories(data.result);
      } catch (error) {
        setCategoryErrorMessage(
          error instanceof Error ? error.message : "Failed to load categories."
        );
      }
    }

    fetchCategories();
  }, []);

  useEffect(() => {
    async function fetchFavorites() {
      const token = getValidToken();

      if (!token) {
        setFavoriteRecipeIds(new Set());
        return;
      }

      try {
        setFavoriteRecipeIds(await loadFavoriteRecipeIds(token));
      } catch (error) {
        setMessage(
          error instanceof Error ? error.message : "Favorite recipes could not be loaded."
        );
      }
    }

    fetchFavorites();
  }, []);

  useEffect(() => {
    async function fetchRecipes() {
      try {
        setIsLoading(true);
        setErrorMessage(null);

        const queryParams = new URLSearchParams();
        const trimmedSearchText = searchText.trim();

        if (trimmedSearchText) {
          queryParams.set("search", trimmedSearchText);
        }

        if (selectedCategoryId) {
          queryParams.set("categoryId", selectedCategoryId.toString());
        }

        const queryString = queryParams.toString();
        const response = await fetch(
          `${API_URL}/api/recipe${queryString ? `?${queryString}` : ""}`
        );
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

    if (!isCategoryFilterReady) {
      return;
    }

    const debounceTimer = window.setTimeout(fetchRecipes, 400);

    return () => window.clearTimeout(debounceTimer);
  }, [isCategoryFilterReady, searchText, selectedCategoryId]);

  function handleSearchChange(value: string) {
    setSearchText(value);
    setIsLoading(true);
  }

  function handleCategoryChange(categoryId: number | null) {
    setSelectedCategoryId(categoryId);
    setIsLoading(true);

    const queryParams = new URLSearchParams(window.location.search);

    if (categoryId) {
      queryParams.set("categoryId", categoryId.toString());
    } else {
      queryParams.delete("categoryId");
    }

    const queryString = queryParams.toString();
    window.history.pushState(null, "", queryString ? `/recipes?${queryString}` : "/recipes");
  }

  async function handleToggleFavorite(recipeId: number) {
    setMessage(null);
    setPendingFavoriteRecipeId(recipeId);

    try {
      const token = getValidToken();

      if (!token) {
        setRedirectPath(pathname);
        router.push("/login");
        return;
      }

      const isFavorite = favoriteRecipeIds.has(recipeId);
      const response = await fetch(
        `${API_URL}/api/favoriterecipe/${recipeId}`,
        {
          method: isFavorite ? "DELETE" : "POST",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0]
          ?? (isFavorite
            ? "Recipe could not be removed from favorites."
            : "Recipe could not be added to favorites.")
        );
      }

      setFavoriteRecipeIds((currentFavoriteRecipeIds) => {
        const nextFavoriteRecipeIds = new Set(currentFavoriteRecipeIds);

        if (isFavorite) {
          nextFavoriteRecipeIds.delete(recipeId);
        } else {
          nextFavoriteRecipeIds.add(recipeId);
        }

        return nextFavoriteRecipeIds;
      });
      setMessage(isFavorite ? "Recipe removed from favorites." : "Recipe saved.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Favorite status could not be updated."
      );
    } finally {
      setPendingFavoriteRecipeId(null);
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
            value={searchText}
            onChange={(event) => handleSearchChange(event.target.value)}
            className="form-input"
          />
        </div>
      </div>

      <div className="app-card p-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-slate-950">
              Categories
            </p>
            <p className="mt-1 text-sm text-slate-600">
              {selectedCategoryId
                ? `Showing ${
                    categories.find((category) => category.id === selectedCategoryId)?.name
                    ?? "selected category"
                  } recipes`
                : "Showing all recipes"}
            </p>
          </div>
          {categoryErrorMessage && (
            <p className="text-sm text-red-700">{categoryErrorMessage}</p>
          )}
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => handleCategoryChange(null)}
            className={selectedCategoryId === null ? "primary-button" : "secondary-button"}
          >
            All recipes
          </button>

          {categories.map((category) => (
            <button
              key={category.id}
              type="button"
              onClick={() => handleCategoryChange(category.id)}
              className={
                selectedCategoryId === category.id ? "primary-button" : "secondary-button"
              }
            >
              {category.name}
            </button>
          ))}
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
              {searchText.trim() || selectedCategoryId
                ? "No recipes found."
                : "No recipes yet. Add your first recipe."}
            </h2>
            <p className="mt-3 text-sm leading-6 text-slate-600">
              {searchText.trim() || selectedCategoryId
                ? "Try another search term, choose another category, or show all recipes."
                : "Recipes created from the Admin dashboard will appear here for everyone to browse."}
            </p>
            {searchText.trim() || selectedCategoryId ? (
              <div className="mt-6 flex flex-wrap justify-center gap-3">
                {searchText.trim() && (
                  <button
                    type="button"
                    onClick={() => handleSearchChange("")}
                    className="secondary-button"
                  >
                    Clear Search
                  </button>
                )}
                {selectedCategoryId && (
                  <button
                    type="button"
                    onClick={() => handleCategoryChange(null)}
                    className="primary-button"
                  >
                    All recipes
                  </button>
                )}
              </div>
            ) : (
              <Link href="/admin" className="primary-button mt-6 inline-flex">
                Go to Admin Dashboard
              </Link>
            )}
          </div>
        </div>
      )}

      {!isLoading && !errorMessage && recipes.length > 0 && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {recipes.map((recipe) => (
            <RecipeCard
              key={recipe.id}
              recipe={recipe}
              isFavorite={favoriteRecipeIds.has(recipe.id)}
              isFavoritePending={pendingFavoriteRecipeId === recipe.id}
              onToggleFavorite={handleToggleFavorite}
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
  isFavorite,
  isFavoritePending,
  onToggleFavorite
}: {
  recipe: Recipe;
  isFavorite: boolean;
  isFavoritePending: boolean;
  onToggleFavorite: (recipeId: number) => void;
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
          onClick={() => onToggleFavorite(recipe.id)}
          disabled={isFavoritePending}
          className={isFavorite ? "primary-button w-full" : "secondary-button w-full"}
        >
          {isFavoritePending ? "Saving..." : isFavorite ? "♥ Saved" : "♡ Save"}
        </button>
      </div>
    </article>
  );
}
