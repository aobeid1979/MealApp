"use client";

import { useEffect, useState } from "react";
import { useParams, usePathname, useRouter } from "next/navigation";

type RecipeDetails = {
  id: number;
  title: string;
  description: string | null;
  imageUrl: string | null;
  preparationTimeMinutes: number;
  cookingTimeMinutes: number;
  servings: number;
  categoryId: number;
  categoryName: string | null;
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
  tools: RecipeTool[];
};

type RecipeIngredient = {
  ingredientId: number;
  ingredientName: string | null;
  quantity: number;
  unitId: number;
  unitName: string | null;
  unitShortName: string | null;
};

type RecipeStep = {
  id: number;
  recipeId: number;
  stepNumber: number;
  instruction: string;
};

type RecipeTool = {
  id: number;
  recipeId: number;
  name: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function RecipeDetailsPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const pathname = usePathname();
  const id = params.id;

  const [recipe, setRecipe] = useState<RecipeDetails | null>(null);
  const [ingredients, setIngredients] = useState<RecipeIngredient[]>([]);
  const [selectedServings, setSelectedServings] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [mealPlanMessage, setMealPlanMessage] = useState<string | null>(null);
  const [isAddingToMealPlan, setIsAddingToMealPlan] = useState(false);
  const [favoriteMessage, setFavoriteMessage] = useState<string | null>(null);
  const [isAddingFavorite, setIsAddingFavorite] = useState(false);

  useEffect(() => {
    async function fetchRecipeDetails() {
      try {
        const response = await fetch(
          `${API_URL}/api/recipe/${id}/details`
        );
        const data = (await response.json()) as ApiResponse<RecipeDetails>;

        if (!data.isSuccess || !response.ok) {
          throw new Error(
            data.errorMessages?.[0] ?? "Recipe details could not be loaded."
          );
        }

        setRecipe(data.result);
        setIngredients(data.result.ingredients);
        setSelectedServings(Math.max(1, data.result.servings));
      } catch (error) {
        setErrorMessage(
          error instanceof Error
            ? error.message
            : "Recipe details could not be loaded."
        );
      } finally {
        setIsLoading(false);
      }
    }

    fetchRecipeDetails();
  }, [id]);

  useEffect(() => {
    if (!recipe) {
      return;
    }

    async function fetchPreview() {
      try {
        const response = await fetch(
          `${API_URL}/api/recipe/${id}/preview?servings=${selectedServings}`
        );
        const data = (await response.json()) as ApiResponse<RecipeDetails>;

        if (!data.isSuccess || !response.ok) {
          throw new Error(
            data.errorMessages?.[0] ?? "Ingredient quantities could not be updated."
          );
        }

        setIngredients(data.result.ingredients);
      } catch (error) {
        setErrorMessage(
          error instanceof Error
            ? error.message
            : "Ingredient quantities could not be updated."
        );
      }
    }

    fetchPreview();
  }, [id, recipe, selectedServings]);

  if (isLoading) {
    return (
      <section className="status-panel">
        Loading recipe details...
      </section>
    );
  }

  if (!recipe || errorMessage) {
    return (
      <section className="error-panel">
        {errorMessage ?? "Recipe details could not be loaded."}
      </section>
    );
  }

  const hasValidImageUrl =
    recipe.imageUrl !== null
    && recipe.imageUrl.trim().length > 0
    && recipe.imageUrl !== "string"
    && URL.canParse(recipe.imageUrl);

  async function handleAddToMealPlan() {
    if (!recipe) {
      return;
    }

    setMealPlanMessage(null);
    setIsAddingToMealPlan(true);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        localStorage.setItem("redirectPath", pathname);
        router.push("/login");
        return;
      }

      const today = new Date().toISOString().split("T")[0];
      const response = await fetch(`${API_URL}/api/mealplan`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer " + token
        },
        body: JSON.stringify({
          recipeId: recipe.id,
          servings: selectedServings,
          plannedDate: today
        })
      });

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Recipe could not be added to meal plan."
        );
      }

      setMealPlanMessage("Recipe added to meal plan.");
    } catch (error) {
      setMealPlanMessage(
        error instanceof Error
          ? error.message
          : "Recipe could not be added to meal plan."
      );
    } finally {
      setIsAddingToMealPlan(false);
    }
  }

  async function handleAddFavorite() {
    if (!recipe) {
      return;
    }

    setFavoriteMessage(null);
    setIsAddingFavorite(true);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        localStorage.setItem("redirectPath", pathname);
        router.push("/login");
        return;
      }

      const response = await fetch(
        `${API_URL}/api/favoriterecipe/${recipe.id}`,
        {
          method: "POST",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Recipe could not be added to favorites."
        );
      }

      setFavoriteMessage("Recipe added to favorites.");
    } catch (error) {
      setFavoriteMessage(
        error instanceof Error
          ? error.message
          : "Recipe could not be added to favorites."
      );
    } finally {
      setIsAddingFavorite(false);
    }
  }

  return (
    <section className="page-section">
      <div className="app-card overflow-hidden">
        {hasValidImageUrl ? (
          <div
            className="h-64 bg-cover bg-center"
            style={{ backgroundImage: `url(${recipe.imageUrl})` }}
            aria-label={recipe.title}
          />
        ) : (
          <div className="flex h-64 items-center justify-center bg-emerald-50 text-sm font-medium text-emerald-700">
            Image placeholder
          </div>
        )}

        <div className="space-y-6 p-6">
          <div>
            <p className="eyebrow">
              {recipe.categoryName ?? "Uncategorized"}
            </p>
            <h1 className="page-title">
              {recipe.title}
            </h1>
            <p className="page-description">
              {recipe.description ?? "No description provided."}
            </p>
          </div>

          <div className="grid gap-4 border-t border-slate-100 pt-6 text-sm text-slate-700 sm:grid-cols-3">
            <div>
              <span className="block text-xs font-semibold uppercase tracking-wide text-slate-500">
                Servings
              </span>
              <div className="mt-2 flex items-center gap-3">
                <button
                  type="button"
                  onClick={() =>
                    setSelectedServings((currentServings) =>
                      Math.max(1, currentServings - 1)
                    )
                  }
                  className="flex h-8 w-8 items-center justify-center rounded-md border border-slate-300 bg-white text-lg font-semibold text-slate-700 transition hover:border-emerald-600 hover:text-emerald-700"
                >
                  -
                </button>
                <span className="min-w-8 text-center font-medium text-slate-900">
                  {selectedServings}
                </span>
                <button
                  type="button"
                  onClick={() =>
                    setSelectedServings((currentServings) => currentServings + 1)
                  }
                  className="flex h-8 w-8 items-center justify-center rounded-md border border-slate-300 bg-white text-lg font-semibold text-slate-700 transition hover:border-emerald-600 hover:text-emerald-700"
                >
                  +
                </button>
              </div>
            </div>
            <InfoItem
              label="Preparation"
              value={`${recipe.preparationTimeMinutes} min`}
            />
            <InfoItem label="Cooking" value={`${recipe.cookingTimeMinutes} min`} />
          </div>

          <div className="border-t border-slate-100 pt-6">
            <div className="flex flex-wrap gap-3">
              <button
                type="button"
                onClick={handleAddToMealPlan}
                disabled={isAddingToMealPlan}
                className="primary-button"
              >
                {isAddingToMealPlan ? "Adding..." : "Add to Meal Plan"}
              </button>
              <button
                type="button"
                onClick={handleAddFavorite}
                disabled={isAddingFavorite}
                className="secondary-button"
              >
                {isAddingFavorite ? "Saving..." : "Favorite"}
              </button>
            </div>

            {mealPlanMessage && (
              <p className="mt-3 text-sm text-slate-700">{mealPlanMessage}</p>
            )}
            {favoriteMessage && (
              <p className="mt-3 text-sm text-slate-700">{favoriteMessage}</p>
            )}
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <section className="app-card p-5 lg:col-span-1">
          <h2 className="text-xl font-semibold tracking-normal">Ingredients</h2>
          {ingredients.length === 0 ? (
            <p className="mt-4 text-sm text-slate-600">
              No ingredients added yet.
            </p>
          ) : (
            <ul className="mt-4 space-y-3">
              {ingredients.map((ingredient) => (
                <li
                  key={`${ingredient.ingredientId}-${ingredient.unitId}`}
                  className="flex items-center justify-between gap-4 border-b border-slate-100 pb-3 text-sm last:border-b-0 last:pb-0"
                >
                  <span className="font-medium text-slate-800">
                    {ingredient.ingredientName ?? "Ingredient"}
                  </span>
                  <span className="text-slate-600">
                    {ingredient.quantity}{" "}
                    {ingredient.unitShortName ?? ingredient.unitName ?? ""}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="app-card p-5 lg:col-span-2">
          <h2 className="text-xl font-semibold tracking-normal">Steps</h2>
          {recipe.steps.length === 0 ? (
            <p className="mt-4 text-sm text-slate-600">No steps added yet.</p>
          ) : (
            <ol className="mt-4 space-y-4">
              {[...recipe.steps]
                .sort((first, second) => first.stepNumber - second.stepNumber)
                .map((step) => (
                  <li key={step.id} className="flex gap-4">
                    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-emerald-700 text-sm font-semibold text-white">
                      {step.stepNumber}
                    </span>
                    <p className="pt-1 text-sm leading-6 text-slate-700">
                      {step.instruction}
                    </p>
                  </li>
                ))}
            </ol>
          )}
        </section>
      </div>

      <section className="app-card p-5">
        <h2 className="text-xl font-semibold tracking-normal">Tools</h2>
        {recipe.tools.length === 0 ? (
          <p className="mt-4 text-sm text-slate-600">No tools added yet.</p>
        ) : (
          <div className="mt-4 flex flex-wrap gap-2">
            {recipe.tools.map((tool) => (
              <span
                key={tool.id}
                className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700"
              >
                {tool.name}
              </span>
            ))}
          </div>
        )}
      </section>
    </section>
  );
}

function InfoItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="block text-xs font-semibold uppercase tracking-wide text-slate-500">
        {label}
      </span>
      <span className="mt-1 block font-medium text-slate-900">{value}</span>
    </div>
  );
}
