"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

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

const heroImageUrl =
  "https://images.unsplash.com/photo-1498837167922-ddd27525d352";

export default function Home() {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    async function loadHomeData() {
      try {
        const [recipeResponse, categoryResponse] = await Promise.all([
          fetch(`${API_URL}/api/recipe`),
          fetch(`${API_URL}/api/category`)
        ]);

        const recipeData = (await recipeResponse.json()) as ApiResponse<Recipe[]>;
        const categoryData = (await categoryResponse.json()) as ApiResponse<Category[]>;

        if (!recipeData.isSuccess || !recipeResponse.ok) {
          throw new Error(recipeData.errorMessages?.[0] ?? "Recipes could not be loaded.");
        }

        if (!categoryData.isSuccess || !categoryResponse.ok) {
          throw new Error(
            categoryData.errorMessages?.[0] ?? "Categories could not be loaded."
          );
        }

        setRecipes(recipeData.result);
        setCategories(categoryData.result);
      } catch (error) {
        setErrorMessage(
          error instanceof Error ? error.message : "Home page data could not be loaded."
        );
      } finally {
        setIsLoading(false);
      }
    }

    loadHomeData();
  }, []);

  const featuredRecipes = useMemo(() => recipes.slice(-4).reverse(), [recipes]);

  return (
    <div className="space-y-14">
      <section className="overflow-hidden rounded-lg bg-white shadow-sm">
        <div
          className="relative min-h-[520px] bg-cover bg-center"
          style={{ backgroundImage: `url(${heroImageUrl})` }}
        >
          <div className="absolute inset-0 bg-slate-950/45" />
          <div className="relative flex min-h-[520px] max-w-3xl flex-col justify-center px-6 py-12 text-white sm:px-10">
            <p className="text-sm font-semibold uppercase tracking-wide text-emerald-100">
              MealApp
            </p>
            <h1 className="mt-3 text-4xl font-bold tracking-normal sm:text-5xl">
              Plan comforting meals without the kitchen chaos
            </h1>
            <p className="mt-5 max-w-2xl text-lg leading-8 text-slate-100">
              Browse recipes, scale servings, build meal plans, and turn them into
              organized shopping lists.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link href="/recipes" className="primary-button bg-emerald-600 hover:bg-emerald-700">
                Browse Recipes
              </Link>
              <Link
                href="/meal-plan"
                className="rounded-md border border-white/70 bg-white/10 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-white hover:text-slate-950"
              >
                View Meal Plan
              </Link>
            </div>
          </div>
        </div>
      </section>

      <section className="space-y-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="eyebrow">Featured Recipes</p>
            <h2 className="page-title">Fresh from the kitchen</h2>
          </div>
          <Link href="/recipes" className="secondary-button w-fit">
            See all recipes
          </Link>
        </div>

        {isLoading && <div className="status-panel">Loading featured recipes...</div>}
        {errorMessage && <div className="error-panel">{errorMessage}</div>}

        {!isLoading && !errorMessage && featuredRecipes.length === 0 && (
          <div className="status-panel">No recipes found yet.</div>
        )}

        {!isLoading && !errorMessage && featuredRecipes.length > 0 && (
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {featuredRecipes.map((recipe) => (
              <FeaturedRecipeCard key={recipe.id} recipe={recipe} />
            ))}
          </div>
        )}
      </section>

      <section className="grid gap-5 md:grid-cols-3">
        <InfoStep number="1" title="Choose recipes" text="Find meals that match your time, servings, and mood." />
        <InfoStep number="2" title="Plan your week" text="Add recipes to your meal plan with the servings you need." />
        <InfoStep number="3" title="Shop smarter" text="Generate one shopping list with merged ingredients." />
      </section>

      <section className="rounded-lg bg-emerald-900 px-6 py-10 text-white sm:px-8">
        <div className="grid gap-8 lg:grid-cols-[1fr_1.4fr] lg:items-center">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wide text-emerald-200">
              Benefits
            </p>
            <h2 className="mt-2 text-3xl font-bold tracking-normal">
              Built for everyday home cooking
            </h2>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Benefit title="Less waste" text="Buy what your recipes actually need." />
            <Benefit title="Flexible servings" text="Scale ingredients before you cook." />
            <Benefit title="Clear prep" text="Keep ingredients, steps, and tools together." />
          </div>
        </div>
      </section>

      <section className="space-y-6">
        <div>
          <p className="eyebrow">Categories</p>
          <h2 className="page-title">Find the right kind of meal</h2>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {categories.slice(0, 8).map((category) => (
            <Link
              key={category.id}
              href={`/recipes?categoryId=${category.id}`}
              className="app-card interactive-card block p-5"
            >
              <h3 className="text-lg font-semibold tracking-normal text-slate-950">
                {category.name}
              </h3>
              <p className="mt-2 text-sm leading-6 text-slate-600">
                {category.description ?? "Recipes ready to explore."}
              </p>
            </Link>
          ))}
        </div>
      </section>

      <section className="grid gap-6 lg:grid-cols-2 lg:items-center">
        <div className="space-y-4">
          <p className="eyebrow">About</p>
          <h2 className="page-title">A calm place for meals, plans, and shopping</h2>
          <p className="text-base leading-7 text-slate-700">
            MealApp brings recipe browsing, serving previews, meal planning, and
            shopping lists into one simple workflow. It is designed for the real
            rhythm of a home kitchen: quick changes, practical ingredients, and
            lists that stay organized.
          </p>
        </div>
        <div className="app-card overflow-hidden">
          <div
            className="h-80 bg-cover bg-center"
            style={{
              backgroundImage:
                "url(https://images.unsplash.com/photo-1556911220-bff31c812dba)"
            }}
            aria-label="Kitchen preparation"
          />
        </div>
      </section>

      <section className="grid gap-5 rounded-lg bg-white p-6 shadow-sm md:grid-cols-[1.4fr_1fr] md:items-center sm:p-8">
        <div>
          <p className="eyebrow">Contact</p>
          <h2 className="page-title">Need help with your meal planning?</h2>
          <p className="page-description">
            Reach out for support, recipe ideas, or feedback about the app.
          </p>
        </div>
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-5">
          <p className="text-sm font-semibold text-slate-950">Support</p>
          <Link
            href="/support"
            className="mt-2 inline-flex text-sm font-medium text-emerald-700 transition hover:text-emerald-800"
          >
            meallist.support@gmail.com
          </Link>
          <p className="mt-4 text-sm font-semibold text-slate-950">Hours</p>
          <p className="mt-2 text-sm text-slate-600">Monday to Friday, 9:00-17:00</p>
        </div>
      </section>

      <footer className="border-t border-slate-200 py-6 text-sm text-slate-600">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p>MealApp</p>
          <div className="flex gap-4">
            <Link href="/recipes" className="hover:text-emerald-700">
              Recipes
            </Link>
            <Link href="/login" className="hover:text-emerald-700">
              Login
            </Link>
            <Link href="/register" className="hover:text-emerald-700">
              Register
            </Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

function FeaturedRecipeCard({ recipe }: { recipe: Recipe }) {
  const hasValidImageUrl =
    recipe.imageUrl !== null
    && recipe.imageUrl.trim().length > 0
    && recipe.imageUrl !== "string"
    && URL.canParse(recipe.imageUrl);

  return (
    <article className="app-card interactive-card overflow-hidden">
      {hasValidImageUrl ? (
        <div
          className="h-44 bg-cover bg-center"
          style={{ backgroundImage: `url(${recipe.imageUrl})` }}
          aria-label={recipe.title}
        />
      ) : (
        <div className="flex h-44 items-center justify-center bg-emerald-50 text-sm font-medium text-emerald-700">
          Recipe image
        </div>
      )}

      <div className="space-y-4 p-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
            {recipe.categoryName ?? "Recipe"}
          </p>
          <h3 className="mt-1 text-lg font-semibold tracking-normal text-slate-950">
            {recipe.title}
          </h3>
          <p className="mt-2 line-clamp-3 text-sm leading-6 text-slate-600">
            {recipe.description ?? "A meal ready to add to your plan."}
          </p>
        </div>

        <div className="flex justify-between border-t border-slate-100 pt-4 text-sm text-slate-600">
          <span>{recipe.servings} servings</span>
          <span>{recipe.preparationTimeMinutes} min prep</span>
        </div>

        <Link href={`/recipes/${recipe.id}`} className="primary-button block text-center">
          View Recipe
        </Link>
      </div>
    </article>
  );
}

function InfoStep({ number, title, text }: { number: string; title: string; text: string }) {
  return (
    <div className="app-card p-6">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-100 text-sm font-bold text-emerald-800">
        {number}
      </div>
      <h3 className="mt-5 text-lg font-semibold tracking-normal text-slate-950">
        {title}
      </h3>
      <p className="mt-2 text-sm leading-6 text-slate-600">{text}</p>
    </div>
  );
}

function Benefit({ title, text }: { title: string; text: string }) {
  return (
    <div className="rounded-lg bg-white/10 p-4">
      <h3 className="font-semibold tracking-normal">{title}</h3>
      <p className="mt-2 text-sm leading-6 text-emerald-50">{text}</p>
    </div>
  );
}
