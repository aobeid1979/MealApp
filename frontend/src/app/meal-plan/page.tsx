"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

type MealPlanItem = {
  id: number;
  recipeId: number;
  recipeTitle: string | null;
  recipeImageUrl: string | null;
  servings: number;
  plannedDate: string;
  createdAt: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function MealPlanPage() {
  const router = useRouter();
  const pathname = usePathname();
  const [mealPlans, setMealPlans] = useState<MealPlanItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [editingMealPlanId, setEditingMealPlanId] = useState<number | null>(null);
  const [editServings, setEditServings] = useState("");
  const [editPlannedDate, setEditPlannedDate] = useState("");

  useEffect(() => {
    async function fetchMealPlans() {
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

      try {
        const response = await fetch(`${API_URL}/api/mealplan/my`, {
          headers: {
            Authorization: "Bearer " + token
          }
        });

        const data = (await response.json()) as ApiResponse<MealPlanItem[]>;

        if (!data.isSuccess || !response.ok) {
          throw new Error(
            data.errorMessages?.[0] ?? "Meal plan could not be loaded."
          );
        }

        setMealPlans(data.result);
      } catch (error) {
        setErrorMessage(
          error instanceof Error ? error.message : "Meal plan could not be loaded."
        );
      } finally {
        setIsLoading(false);
      }
    }

    fetchMealPlans();
  }, [pathname, router]);

  async function handleRemove(id: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(`${API_URL}/api/mealplan/${id}`, {
        method: "DELETE",
        headers: {
          Authorization: "Bearer " + token
        }
      });

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Meal plan item could not be removed.");
      }

      setMealPlans((currentMealPlans) =>
        currentMealPlans.filter((mealPlan) => mealPlan.id !== id)
      );
      setMessage("Meal plan item removed.");
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Meal plan item could not be removed."
      );
    }
  }

  async function refreshMealPlans() {
    const token = localStorage.getItem("token");

    if (!token) {
      throw new Error("Login token was not found.");
    }

    const response = await fetch(`${API_URL}/api/mealplan/my`, {
      headers: {
        Authorization: "Bearer " + token
      }
    });

    const data = (await response.json()) as ApiResponse<MealPlanItem[]>;

    if (!data.isSuccess || !response.ok) {
      throw new Error(data.errorMessages?.[0] ?? "Meal plan could not be loaded.");
    }

    setMealPlans(data.result);
  }

  async function handleFilterByRange() {
    setMessage(null);
    setErrorMessage(null);
    setIsLoading(true);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `${API_URL}/api/mealplan/my/range?from=${startDate}&to=${endDate}`,
        {
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<MealPlanItem[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Meal plan range could not be loaded.");
      }

      setMealPlans(data.result);
      setMessage("Meal plan range loaded.");
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Meal plan range could not be loaded."
      );
    } finally {
      setIsLoading(false);
    }
  }

  async function handleClearRange() {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `${API_URL}/api/mealplan/my/range?from=${startDate}&to=${endDate}`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Meal plan range could not be cleared.");
      }

      await refreshMealPlans();
      setMessage("Selected range cleared.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Meal plan range could not be cleared."
      );
    }
  }

  function startEditingMealPlan(mealPlan: MealPlanItem) {
    setMessage(null);
    setEditingMealPlanId(mealPlan.id);
    setEditServings(String(mealPlan.servings));
    setEditPlannedDate(toDateInputValue(mealPlan.plannedDate));
  }

  async function handleUpdateMealPlan(mealPlan: MealPlanItem) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `${API_URL}/api/mealplan/${mealPlan.id}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            recipeId: mealPlan.recipeId,
            servings: Number(editServings),
            plannedDate: editPlannedDate
          })
        }
      );

      const data = (await response.json()) as ApiResponse<MealPlanItem>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Meal plan item could not be updated.");
      }

      setMealPlans((currentMealPlans) =>
        currentMealPlans.map((currentMealPlan) =>
          currentMealPlan.id === mealPlan.id ? data.result : currentMealPlan
        )
      );
      setEditingMealPlanId(null);
      setEditServings("");
      setEditPlannedDate("");
      setMessage("Meal plan item updated.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Meal plan item could not be updated."
      );
    }
  }

  async function handleGenerateShoppingList() {
    setMessage(null);
    setIsGenerating(true);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(`${API_URL}/api/shoppinglist/generate`, {
        method: "POST",
        headers: {
          Authorization: "Bearer " + token
        }
      });

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Shopping list could not be generated.");
      }

      setMessage("Shopping list generated successfully.");
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Shopping list could not be generated."
      );
    } finally {
      setIsGenerating(false);
    }
  }

  return (
    <section className="page-section">
      <div className="page-heading">
        <p className="eyebrow">Meal Plan</p>
        <h1 className="page-title">My Meal Plan</h1>
        <p className="page-description">
          View recipes you have added to your meal plan.
        </p>
      </div>

      {message && (
        <div className="status-panel">
          {message}
        </div>
      )}

      {errorMessage && (
        <div className="error-panel">
          {errorMessage}
        </div>
      )}

      <button
        type="button"
        onClick={handleGenerateShoppingList}
        disabled={isGenerating}
        className="primary-button w-fit"
      >
        {isGenerating ? "Generating..." : "Generate Shopping List"}
      </button>

      <div className="app-card p-5">
        <h2 className="text-lg font-semibold tracking-normal">Date range</h2>
        <div className="mt-4 grid gap-4 md:grid-cols-[1fr_1fr_auto_auto] md:items-end">
          <label className="form-label">
            Start date
            <input
              type="date"
              value={startDate}
              onChange={(event) => setStartDate(event.target.value)}
              className="form-input"
            />
          </label>
          <label className="form-label">
            End date
            <input
              type="date"
              value={endDate}
              onChange={(event) => setEndDate(event.target.value)}
              className="form-input"
            />
          </label>
          <button
            type="button"
            onClick={handleFilterByRange}
            className="secondary-button"
          >
            Filter
          </button>
          <button
            type="button"
            onClick={handleClearRange}
            className="danger-button"
          >
            Clear selected range
          </button>
        </div>
      </div>

      {isLoading && (
        <div className="status-panel">
          Loading meal plan...
        </div>
      )}

      {!isLoading && !errorMessage && mealPlans.length === 0 && (
        <div className="status-panel">
          No planned recipes yet.
        </div>
      )}

      {!isLoading && !errorMessage && mealPlans.length > 0 && (
        <div className="space-y-3">
          {mealPlans.map((mealPlan) => (
            <article
              key={mealPlan.id}
              className="app-card interactive-card flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between"
            >
              <div>
                <h2 className="text-lg font-semibold tracking-normal">
                  {mealPlan.recipeTitle ?? "Recipe"}
                </h2>
                <p className="mt-2 text-sm text-slate-600">
                  {mealPlan.servings} servings · Planned for{" "}
                  {formatDate(mealPlan.plannedDate)}
                </p>
                {editingMealPlanId === mealPlan.id && (
                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <label className="form-label">
                      Servings
                      <input
                        type="number"
                        min="1"
                        max="50"
                        value={editServings}
                        onChange={(event) => setEditServings(event.target.value)}
                        className="form-input"
                      />
                    </label>
                    <label className="form-label">
                      Planned date
                      <input
                        type="date"
                        value={editPlannedDate}
                        onChange={(event) => setEditPlannedDate(event.target.value)}
                        className="form-input"
                      />
                    </label>
                  </div>
                )}
              </div>

              <div className="flex flex-wrap gap-2">
                {editingMealPlanId === mealPlan.id ? (
                  <>
                    <button
                      type="button"
                      onClick={() => handleUpdateMealPlan(mealPlan)}
                      className="primary-button"
                    >
                      Save
                    </button>
                    <button
                      type="button"
                      onClick={() => setEditingMealPlanId(null)}
                      className="secondary-button"
                    >
                      Cancel
                    </button>
                  </>
                ) : (
                  <button
                    type="button"
                    onClick={() => startEditingMealPlan(mealPlan)}
                    className="secondary-button"
                  >
                    Edit
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => handleRemove(mealPlan.id)}
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
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    year: "numeric",
    month: "short",
    day: "numeric"
  }).format(new Date(value));
}

function toDateInputValue(value: string) {
  return new Date(value).toISOString().split("T")[0];
}
