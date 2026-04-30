"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

type ProfileInfo = {
  name: string;
  email: string;
  role: string;
};

type FavoriteRecipe = {
  id: number;
  recipeId: number;
  recipeTitle: string | null;
  recipeDescription: string | null;
  recipeImageUrl: string | null;
  preparationTimeMinutes: number;
  cookingTimeMinutes: number;
  servings: number;
  categoryId: number;
  categoryName: string | null;
  createdAt: string;
};

type MealPlanItem = {
  id: number;
  recipeTitle: string | null;
  servings: number;
  plannedDate: string;
};

type ShoppingList = {
  id: number;
  name: string;
  createdAt: string;
  items: ShoppingListItem[];
};

type ShoppingListItem = {
  id: number;
  isChecked: boolean;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function ProfilePage() {
  const router = useRouter();
  const pathname = usePathname();
  const [profile, setProfile] = useState<ProfileInfo>({
    name: "",
    email: "",
    role: ""
  });
  const [favorites, setFavorites] = useState<FavoriteRecipe[]>([]);
  const [mealPlans, setMealPlans] = useState<MealPlanItem[]>([]);
  const [shoppingLists, setShoppingLists] = useState<ShoppingList[]>([]);
  const [isLoadingFavorites, setIsLoadingFavorites] = useState(true);
  const [isLoadingSummaries, setIsLoadingSummaries] = useState(true);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    const token = localStorage.getItem("token");
    const role = localStorage.getItem("role");

    if (!token) {
      localStorage.setItem("redirectPath", pathname);
      router.push("/login");
      return;
    }

    if (role !== "User" && role !== "Admin") {
      router.push("/");
      return;
    }

    const authToken = token;

    // localStorage is available only after the page mounts in the browser.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setProfile({
      name: localStorage.getItem("name") ?? "",
      email: localStorage.getItem("email") ?? "",
      role: localStorage.getItem("role") ?? ""
    });

    async function fetchProfileData() {
      try {
        const [favoriteRecipes, userMealPlans, userShoppingLists] = await Promise.all([
          loadFavorites(authToken),
          loadMealPlans(authToken),
          loadShoppingLists(authToken)
        ]);

        setFavorites(favoriteRecipes);
        setMealPlans(userMealPlans);
        setShoppingLists(userShoppingLists);
      } catch (error) {
        setMessage(
          error instanceof Error ? error.message : "Profile data could not be loaded."
        );
      } finally {
        setIsLoadingFavorites(false);
        setIsLoadingSummaries(false);
      }
    }

    fetchProfileData();
  }, [pathname, router]);

  async function handleRemoveFavorite(recipeId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        localStorage.setItem("redirectPath", pathname);
        router.push("/login");
        return;
      }

      const response = await fetch(
        `${API_URL}/api/favoriterecipe/${recipeId}`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Favorite recipe could not be removed.");
      }

      setFavorites(await loadFavorites(token));
      setMessage("Favorite recipe removed.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Favorite recipe could not be removed."
      );
    }
  }

  return (
    <section className="page-section">
      <div className="page-heading">
        <p className="eyebrow">Profile</p>
        <h1 className="page-title">My Profile</h1>
        <p className="page-description">
          View your saved account information for this browser session.
        </p>
      </div>

      {message && <div className="status-panel">{message}</div>}

      <div className="app-card p-6 sm:p-8">
        <div className="grid gap-4 sm:grid-cols-3">
          <ProfileField label="Name" value={profile.name || "Not available"} />
          <ProfileField label="Email" value={profile.email || "Not available"} />
          <ProfileField label="Role" value={profile.role || "Not available"} />
        </div>
      </div>

      <section className="app-card p-5">
        <h2 className="text-lg font-semibold tracking-normal text-slate-950">
          My Favorite Recipes
        </h2>

        {isLoadingFavorites && (
          <p className="mt-4 text-sm text-slate-600">Loading favorites...</p>
        )}

        {!isLoadingFavorites && favorites.length === 0 && (
          <p className="mt-4 text-sm text-slate-600">
            No favorite recipes yet.
          </p>
        )}

        {!isLoadingFavorites && favorites.length > 0 && (
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            {favorites.map((favorite) => (
              <article
                key={favorite.id}
                className="rounded-lg border border-slate-200 bg-slate-50 p-4"
              >
                <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
                  {favorite.categoryName ?? "Recipe"}
                </p>
                <h3 className="mt-1 text-base font-semibold tracking-normal text-slate-950">
                  {favorite.recipeTitle ?? "Recipe"}
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600">
                  {favorite.recipeDescription ?? "No description provided."}
                </p>
                <div className="mt-4 flex flex-wrap gap-2">
                  <Link
                    href={`/recipes/${favorite.recipeId}`}
                    className="primary-button"
                  >
                    View
                  </Link>
                  <button
                    type="button"
                    onClick={() => handleRemoveFavorite(favorite.recipeId)}
                    className="danger-button"
                  >
                    Remove
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <div className="grid gap-5 md:grid-cols-2">
        <section className="app-card p-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <h2 className="text-lg font-semibold tracking-normal text-slate-950">
                My Meal Plans
              </h2>
              <p className="mt-2 text-sm leading-6 text-slate-600">
                {isLoadingSummaries
                  ? "Loading meal plan summary..."
                  : `${mealPlans.length} planned recipe${mealPlans.length === 1 ? "" : "s"}.`}
              </p>
            </div>
            <Link href="/meal-plan" className="secondary-button w-fit">
              View my meal plan
            </Link>
          </div>

          {!isLoadingSummaries && mealPlans.length > 0 && (
            <ul className="mt-4 space-y-3">
              {mealPlans.slice(0, 3).map((mealPlan) => (
                <li
                  key={mealPlan.id}
                  className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm"
                >
                  <span className="font-medium text-slate-900">
                    {mealPlan.recipeTitle ?? "Recipe"}
                  </span>
                  <span className="mt-1 block text-slate-600">
                    {mealPlan.servings} servings · {formatDate(mealPlan.plannedDate)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="app-card p-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <h2 className="text-lg font-semibold tracking-normal text-slate-950">
                My Shopping Lists
              </h2>
              <p className="mt-2 text-sm leading-6 text-slate-600">
                {isLoadingSummaries
                  ? "Loading shopping list summary..."
                  : `${shoppingLists.length} shopping list${shoppingLists.length === 1 ? "" : "s"}.`}
              </p>
            </div>
            <Link href="/shopping-list" className="secondary-button w-fit">
              View my shopping lists
            </Link>
          </div>

          {!isLoadingSummaries && shoppingLists.length > 0 && (
            <ul className="mt-4 space-y-3">
              {shoppingLists.slice(0, 3).map((shoppingList) => {
                const checkedItems = shoppingList.items.filter((item) => item.isChecked).length;

                return (
                  <li
                    key={shoppingList.id}
                    className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm"
                  >
                    <span className="font-medium text-slate-900">
                      {shoppingList.name}
                    </span>
                    <span className="mt-1 block text-slate-600">
                      {checkedItems}/{shoppingList.items.length} items checked
                    </span>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      </div>
    </section>
  );
}

async function loadFavorites(token: string) {
  const response = await fetch(`${API_URL}/api/favoriterecipe/my`, {
    headers: {
      Authorization: "Bearer " + token
    }
  });

  const data = (await response.json()) as ApiResponse<FavoriteRecipe[]>;

  if (!data.isSuccess || !response.ok) {
    throw new Error(data.errorMessages?.[0] ?? "Favorite recipes could not be loaded.");
  }

  return data.result;
}

async function loadMealPlans(token: string) {
  const response = await fetch(`${API_URL}/api/mealplan/my`, {
    headers: {
      Authorization: "Bearer " + token
    }
  });

  const data = (await response.json()) as ApiResponse<MealPlanItem[]>;

  if (!data.isSuccess || !response.ok) {
    throw new Error(data.errorMessages?.[0] ?? "Meal plans could not be loaded.");
  }

  return data.result;
}

async function loadShoppingLists(token: string) {
  const response = await fetch(`${API_URL}/api/shoppinglist/my`, {
    headers: {
      Authorization: "Bearer " + token
    }
  });

  const data = (await response.json()) as ApiResponse<ShoppingList[]>;

  if (!data.isSuccess || !response.ok) {
    throw new Error(data.errorMessages?.[0] ?? "Shopping lists could not be loaded.");
  }

  return data.result;
}

function ProfileField({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        {label}
      </p>
      <p className="mt-2 text-sm font-medium text-slate-900">{value}</p>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    year: "numeric",
    month: "short",
    day: "numeric"
  }).format(new Date(value));
}
