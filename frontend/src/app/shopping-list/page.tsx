"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

type ShoppingList = {
  id: number;
  name: string;
  createdAt: string;
  items: ShoppingListItem[];
};

type ShoppingListItem = {
  id: number;
  ingredientId: number;
  ingredientName: string | null;
  ingredientCategoryId: number | null;
  ingredientCategoryName: string | null;
  quantity: number;
  unitId: number;
  unitName: string | null;
  unitShortName: string | null;
  isChecked: boolean;
};

type ShoppingListGroup = {
  categoryId: number | null;
  categoryName: string | null;
  items: ShoppingListItem[];
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

export default function ShoppingListPage() {
  const router = useRouter();
  const pathname = usePathname();
  const [shoppingLists, setShoppingLists] = useState<ShoppingList[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [editingItemId, setEditingItemId] = useState<number | null>(null);
  const [editQuantity, setEditQuantity] = useState("");
  const [editUnitId, setEditUnitId] = useState("");
  const [renamingListId, setRenamingListId] = useState<number | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [groupedShoppingListId, setGroupedShoppingListId] = useState<number | null>(null);
  const [groupedItems, setGroupedItems] = useState<ShoppingListGroup[]>([]);
  const [isLoadingGroupedItems, setIsLoadingGroupedItems] = useState(false);

  useEffect(() => {
    async function fetchShoppingLists() {
      const token = localStorage.getItem("token");

      if (!token) {
        localStorage.setItem("redirectPath", pathname);
        router.push("/login");
        return;
      }

      try {
        setShoppingLists(await loadShoppingLists());
      } catch (error) {
        setErrorMessage(
          error instanceof Error
            ? error.message
            : "Shopping lists could not be loaded."
        );
      } finally {
        setIsLoading(false);
      }
    }

    fetchShoppingLists();
  }, [pathname, router]);

  async function handleToggleItem(itemId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/items/${itemId}/toggle`,
        {
          method: "PATCH",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<ShoppingListItem>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Shopping list item could not be updated."
        );
      }

      setShoppingLists((currentShoppingLists) =>
        currentShoppingLists.map((shoppingList) => ({
          ...shoppingList,
          items: shoppingList.items.map((item) =>
            item.id === itemId ? data.result : item
          )
        }))
      );
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Shopping list item could not be updated."
      );
    }
  }

  function startEditingItem(item: ShoppingListItem) {
    setMessage(null);
    setEditingItemId(item.id);
    setEditQuantity(item.quantity.toString());
    setEditUnitId(item.unitId.toString());
  }

  async function refreshShoppingLists() {
    setShoppingLists(await loadShoppingLists());
  }

  async function handleUpdateItem(itemId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/items/${itemId}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            quantity: Number(editQuantity),
            unitId: Number(editUnitId)
          })
        }
      );

      const data = (await response.json()) as ApiResponse<ShoppingListItem>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Shopping list item could not be updated."
        );
      }

      setEditingItemId(null);
      setEditQuantity("");
      setEditUnitId("");
      await refreshShoppingLists();
      setMessage("Shopping list item updated.");
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Shopping list item could not be updated."
      );
    }
  }

  async function handleDeleteItem(itemId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/items/${itemId}`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Shopping list item could not be deleted."
        );
      }

      await refreshShoppingLists();
      setMessage("Shopping list item deleted.");
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Shopping list item could not be deleted."
      );
    }
  }

  async function handleGroupedView(shoppingListId: number) {
    setMessage(null);
    setIsLoadingGroupedItems(true);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/${shoppingListId}/grouped`,
        {
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<ShoppingListGroup[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Grouped shopping list could not be loaded."
        );
      }

      setGroupedShoppingListId(shoppingListId);
      setGroupedItems(data.result);
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Grouped shopping list could not be loaded."
      );
    } finally {
      setIsLoadingGroupedItems(false);
    }
  }

  function startRenamingList(shoppingList: ShoppingList) {
    setMessage(null);
    setRenamingListId(shoppingList.id);
    setRenameValue(shoppingList.name);
  }

  async function handleRenameList(shoppingListId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/${shoppingListId}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            name: renameValue
          })
        }
      );

      const data = (await response.json()) as ApiResponse<ShoppingList>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Shopping list could not be renamed.");
      }

      setRenamingListId(null);
      setRenameValue("");
      await refreshShoppingLists();
      setMessage("Shopping list renamed.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Shopping list could not be renamed."
      );
    }
  }

  async function handleDeleteList(shoppingListId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/${shoppingListId}`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Shopping list could not be deleted.");
      }

      if (groupedShoppingListId === shoppingListId) {
        setGroupedShoppingListId(null);
        setGroupedItems([]);
      }

      await refreshShoppingLists();
      setMessage("Shopping list deleted.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Shopping list could not be deleted."
      );
    }
  }

  async function handleClearCheckedItems(shoppingListId: number) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/${shoppingListId}/checked-items`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Checked items could not be cleared."
        );
      }

      await refreshShoppingLists();
      setMessage("Checked items cleared.");
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Checked items could not be cleared."
      );
    }
  }

  async function handleSetAllItemsChecked(shoppingListId: number, isChecked: boolean) {
    setMessage(null);

    try {
      const token = localStorage.getItem("token");

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `http://localhost:5280/api/shoppinglist/${shoppingListId}/items/check-state`,
        {
          method: "PATCH",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            isChecked
          })
        }
      );

      const data = (await response.json()) as ApiResponse<ShoppingList>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Shopping list items could not be updated."
        );
      }

      await refreshShoppingLists();
      setMessage(isChecked ? "All items checked." : "All items unchecked.");
    } catch (error) {
      setMessage(
        error instanceof Error
          ? error.message
          : "Shopping list items could not be updated."
      );
    }
  }

  return (
    <section className="page-section">
      <div className="page-heading">
        <p className="eyebrow">Shopping List</p>
        <h1 className="page-title">My Shopping Lists</h1>
        <p className="page-description">
          View generated shopping lists and check off items as you shop.
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

      {isLoading && (
        <div className="status-panel">
          Loading shopping lists...
        </div>
      )}

      {!isLoading && !errorMessage && shoppingLists.length === 0 && (
        <div className="status-panel">
          No shopping lists yet.
        </div>
      )}

      {!isLoading && !errorMessage && shoppingLists.length > 0 && (
        <div className="space-y-5">
          {shoppingLists.map((shoppingList) => (
            <article
              key={shoppingList.id}
              className="app-card p-5"
            >
              <div className="flex flex-col gap-1 border-b border-slate-100 pb-4">
                <h2 className="text-xl font-semibold tracking-normal">
                  {shoppingList.name}
                </h2>
                <p className="text-sm text-slate-600">
                  Created {formatDate(shoppingList.createdAt)}
                </p>
                <button
                  type="button"
                  onClick={() => handleGroupedView(shoppingList.id)}
                  className="secondary-button mt-3 w-fit"
                >
                  Grouped View
                </button>

                <div className="mt-3 flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => startRenamingList(shoppingList)}
                    className="secondary-button px-3 py-1 text-xs"
                  >
                    Rename
                  </button>
                  <button
                    type="button"
                    onClick={() => handleSetAllItemsChecked(shoppingList.id, true)}
                    className="secondary-button px-3 py-1 text-xs"
                  >
                    Check All
                  </button>
                  <button
                    type="button"
                    onClick={() => handleSetAllItemsChecked(shoppingList.id, false)}
                    className="secondary-button px-3 py-1 text-xs"
                  >
                    Uncheck All
                  </button>
                  <button
                    type="button"
                    onClick={() => handleClearCheckedItems(shoppingList.id)}
                    className="secondary-button px-3 py-1 text-xs"
                  >
                    Clear Checked
                  </button>
                  <button
                    type="button"
                    onClick={() => handleDeleteList(shoppingList.id)}
                    className="danger-button px-3 py-1 text-xs"
                  >
                    Delete List
                  </button>
                </div>

                {renamingListId === shoppingList.id && (
                  <div className="mt-3 grid gap-2 sm:grid-cols-[1fr_auto_auto]">
                    <input
                      type="text"
                      value={renameValue}
                      onChange={(event) => setRenameValue(event.target.value)}
                      className="form-input mt-0"
                      aria-label="Shopping list name"
                    />
                    <button
                      type="button"
                      onClick={() => handleRenameList(shoppingList.id)}
                      className="primary-button px-3"
                    >
                      Save
                    </button>
                    <button
                      type="button"
                      onClick={() => setRenamingListId(null)}
                      className="secondary-button px-3"
                    >
                      Cancel
                    </button>
                  </div>
                )}
              </div>

              {groupedShoppingListId === shoppingList.id && (
                <div className="mt-4 rounded-lg border border-emerald-100 bg-emerald-50 p-4">
                  <h3 className="font-semibold text-emerald-900">
                    Grouped View
                  </h3>

                  {isLoadingGroupedItems && (
                    <p className="mt-3 text-sm text-emerald-800">
                      Loading grouped items...
                    </p>
                  )}

                  {!isLoadingGroupedItems && groupedItems.length === 0 && (
                    <p className="mt-3 text-sm text-emerald-800">
                      No grouped items found.
                    </p>
                  )}

                  {!isLoadingGroupedItems && groupedItems.length > 0 && (
                    <div className="mt-4 space-y-4">
                      {groupedItems.map((group) => (
                        <section
                          key={`${group.categoryId ?? "none"}-${group.categoryName ?? "none"}`}
                        >
                          <h4 className="text-sm font-semibold text-emerald-950">
                            {group.categoryName ?? "Uncategorized"}
                          </h4>
                          <ul className="mt-2 space-y-2">
                            {group.items.map((item) => (
                              <li
                                key={item.id}
                                className="flex justify-between gap-4 text-sm text-emerald-900"
                              >
                                <span>{item.ingredientName ?? "Ingredient"}</span>
                                <span>
                                  {item.quantity}{" "}
                                  {item.unitShortName ?? item.unitName ?? ""}
                                </span>
                              </li>
                            ))}
                          </ul>
                        </section>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {shoppingList.items.length === 0 ? (
                <p className="mt-4 text-sm text-slate-600">
                  No items in this shopping list.
                </p>
              ) : (
                <ul className="mt-4 space-y-3">
                  {shoppingList.items.map((item) => (
                    <li
                      key={item.id}
                      className="space-y-3 border-b border-slate-100 pb-3 text-sm last:border-b-0 last:pb-0"
                    >
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <label className="flex items-center gap-3">
                          <input
                            type="checkbox"
                            checked={item.isChecked}
                            onChange={() => handleToggleItem(item.id)}
                            className="h-4 w-4 rounded border-slate-300 text-emerald-700"
                          />
                          <span
                            className={
                              item.isChecked
                                ? "font-medium text-slate-400 line-through"
                                : "font-medium text-slate-800"
                            }
                          >
                            {item.ingredientName ?? "Ingredient"}
                          </span>
                        </label>

                        <div className="flex flex-wrap items-center gap-3">
                          <span className="text-slate-600">
                            {item.quantity}{" "}
                            {item.unitShortName ?? item.unitName ?? ""}
                          </span>
                          <button
                            type="button"
                          onClick={() => startEditingItem(item)}
                          className="secondary-button px-3 py-1 text-xs"
                        >
                          Edit
                        </button>
                          <button
                          type="button"
                          onClick={() => handleDeleteItem(item.id)}
                          className="danger-button px-3 py-1 text-xs"
                        >
                            Delete
                          </button>
                        </div>
                      </div>

                      {editingItemId === item.id && (
                        <div className="grid gap-3 rounded-md bg-slate-50 p-3 sm:grid-cols-[1fr_1fr_auto_auto]">
                          <input
                            type="number"
                            min="0.01"
                            step="0.01"
                            value={editQuantity}
                            onChange={(event) =>
                              setEditQuantity(event.target.value)
                            }
                            className="form-input mt-0"
                            aria-label="Quantity"
                          />
                          <input
                            type="number"
                            min="1"
                            value={editUnitId}
                            onChange={(event) => setEditUnitId(event.target.value)}
                            className="form-input mt-0"
                            aria-label="Unit id"
                          />
                          <button
                            type="button"
                            onClick={() => handleUpdateItem(item.id)}
                            className="primary-button px-3"
                          >
                            Save
                          </button>
                          <button
                            type="button"
                            onClick={() => setEditingItemId(null)}
                            className="secondary-button px-3"
                          >
                            Cancel
                          </button>
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

async function loadShoppingLists() {
  const token = localStorage.getItem("token");

  if (!token) {
    throw new Error("Login token was not found.");
  }

  const response = await fetch("http://localhost:5280/api/shoppinglist/my", {
    headers: {
      Authorization: "Bearer " + token
    }
  });

  const data = (await response.json()) as ApiResponse<ShoppingList[]>;

  if (!data.isSuccess || !response.ok) {
    throw new Error(
      data.errorMessages?.[0] ?? "Shopping lists could not be loaded."
    );
  }

  return data.result;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    year: "numeric",
    month: "short",
    day: "numeric"
  }).format(new Date(value));
}
