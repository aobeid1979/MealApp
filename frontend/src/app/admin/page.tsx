"use client";

import { FormEvent, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { getRole, getValidToken, setRedirectPath } from "@/lib/auth";

type Category = {
  id: number;
  name: string;
  description: string | null;
};

type IngredientCategory = {
  id: number;
  name: string;
  description: string | null;
};

type Unit = {
  id: number;
  name: string;
  shortName: string;
};

type Recipe = {
  id: number;
  title: string;
  description: string | null;
  imageUrl: string | null;
  preparationTimeMinutes: number;
  cookingTimeMinutes: number;
  servings: number;
  categoryId: number;
};

type Ingredient = {
  id: number;
  name: string;
  description: string | null;
  imageUrl: string | null;
  ingredientCategoryId: number;
  ingredientCategoryName: string | null;
};

type RecipeIngredient = {
  id: number;
  recipeId: number;
  ingredientId: number;
  ingredientName: string | null;
  quantity: number;
  unitId: number;
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

type CategoryDeleteConflictResult = {
  recipeCount?: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

const categoriesUrl = `${API_URL}/api/category`;
const ingredientCategoriesUrl = `${API_URL}/api/ingredientcategory`;
const unitsUrl = `${API_URL}/api/unit`;
const recipesUrl = `${API_URL}/api/recipe`;
const ingredientsUrl = `${API_URL}/api/ingredient`;

export default function AdminPage() {
  const router = useRouter();
  const pathname = usePathname();
  const [isCheckingAccess, setIsCheckingAccess] = useState(true);
  const [isLoadingCategories, setIsLoadingCategories] = useState(true);
  const [isLoadingIngredientCategories, setIsLoadingIngredientCategories] = useState(true);
  const [isLoadingIngredients, setIsLoadingIngredients] = useState(true);
  const [isLoadingUnits, setIsLoadingUnits] = useState(true);
  const [isLoadingRecipes, setIsLoadingRecipes] = useState(true);
  const [isLoadingRecipeIngredients, setIsLoadingRecipeIngredients] = useState(false);
  const [isLoadingRecipeSteps, setIsLoadingRecipeSteps] = useState(false);
  const [isLoadingRecipeTools, setIsLoadingRecipeTools] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [ingredientCategories, setIngredientCategories] = useState<IngredientCategory[]>([]);
  const [units, setUnits] = useState<Unit[]>([]);
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [selectedRecipe, setSelectedRecipe] = useState<Recipe | null>(null);
  const [selectedStepsRecipe, setSelectedStepsRecipe] = useState<Recipe | null>(null);
  const [selectedToolsRecipe, setSelectedToolsRecipe] = useState<Recipe | null>(null);
  const [recipeIngredients, setRecipeIngredients] = useState<RecipeIngredient[]>([]);
  const [recipeSteps, setRecipeSteps] = useState<RecipeStep[]>([]);
  const [recipeTools, setRecipeTools] = useState<RecipeTool[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [ingredientCategoryName, setIngredientCategoryName] = useState("");
  const [ingredientCategoryDescription, setIngredientCategoryDescription] = useState("");
  const [ingredientName, setIngredientName] = useState("");
  const [ingredientDescription, setIngredientDescription] = useState("");
  const [ingredientImageUrl, setIngredientImageUrl] = useState("");
  const [ingredientCategoryId, setIngredientCategoryId] = useState("");
  const [unitName, setUnitName] = useState("");
  const [unitShortName, setUnitShortName] = useState("");
  const [selectedIngredientId, setSelectedIngredientId] = useState("");
  const [selectedUnitId, setSelectedUnitId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [stepNumber, setStepNumber] = useState("");
  const [instruction, setInstruction] = useState("");
  const [toolName, setToolName] = useState("");
  const [recipeTitle, setRecipeTitle] = useState("");
  const [recipeDescription, setRecipeDescription] = useState("");
  const [recipeImageUrl, setRecipeImageUrl] = useState("");
  const [preparationTimeMinutes, setPreparationTimeMinutes] = useState("");
  const [cookingTimeMinutes, setCookingTimeMinutes] = useState("");
  const [servings, setServings] = useState("");
  const [recipeCategoryId, setRecipeCategoryId] = useState("");
  const [editingCategoryId, setEditingCategoryId] = useState<number | null>(null);
  const [editingIngredientCategoryId, setEditingIngredientCategoryId] = useState<number | null>(null);
  const [editingIngredientId, setEditingIngredientId] = useState<number | null>(null);
  const [editingUnitId, setEditingUnitId] = useState<number | null>(null);
  const [editingRecipeId, setEditingRecipeId] = useState<number | null>(null);
  const [editingStepId, setEditingStepId] = useState<number | null>(null);
  const [editingToolId, setEditingToolId] = useState<number | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmittingIngredientCategory, setIsSubmittingIngredientCategory] = useState(false);
  const [isSubmittingIngredient, setIsSubmittingIngredient] = useState(false);
  const [isSubmittingUnit, setIsSubmittingUnit] = useState(false);
  const [isSubmittingRecipe, setIsSubmittingRecipe] = useState(false);
  const [isSubmittingRecipeIngredient, setIsSubmittingRecipeIngredient] = useState(false);
  const [isSubmittingRecipeStep, setIsSubmittingRecipeStep] = useState(false);
  const [isSubmittingRecipeTool, setIsSubmittingRecipeTool] = useState(false);

  useEffect(() => {
    const token = getValidToken();
    const role = getRole();

    if (!token) {
      setRedirectPath(pathname);
      router.push("/login");
      return;
    }

    if (role !== "Admin") {
      router.push("/");
      return;
    }

    // Auth storage is only available in the browser, so access is checked after mount.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsCheckingAccess(false);
    loadCategories();
    loadIngredientCategories();
    loadUnits();
    loadRecipes();
    loadIngredients();
  }, [pathname, router]);

  async function loadCategories() {
    setIsLoadingCategories(true);
    setErrorMessage(null);

    try {
      const response = await fetch(categoriesUrl);
      const data = (await response.json()) as ApiResponse<Category[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Categories could not be loaded.");
      }

      setCategories(data.result);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Categories could not be loaded."
      );
    } finally {
      setIsLoadingCategories(false);
    }
  }

  async function loadIngredientCategories() {
    setIsLoadingIngredientCategories(true);
    setErrorMessage(null);

    try {
      const response = await fetch(ingredientCategoriesUrl);
      const data = (await response.json()) as ApiResponse<IngredientCategory[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Ingredient categories could not be loaded."
        );
      }

      setIngredientCategories(data.result);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Ingredient categories could not be loaded."
      );
    } finally {
      setIsLoadingIngredientCategories(false);
    }
  }

  async function loadUnits() {
    setIsLoadingUnits(true);
    setErrorMessage(null);

    try {
      const response = await fetch(unitsUrl);
      const data = (await response.json()) as ApiResponse<Unit[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Units could not be loaded.");
      }

      setUnits(data.result);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Units could not be loaded."
      );
    } finally {
      setIsLoadingUnits(false);
    }
  }

  async function loadRecipes() {
    setIsLoadingRecipes(true);
    setErrorMessage(null);

    try {
      const response = await fetch(recipesUrl);
      const data = (await response.json()) as ApiResponse<Recipe[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipes could not be loaded.");
      }

      setRecipes(data.result);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipes could not be loaded."
      );
    } finally {
      setIsLoadingRecipes(false);
    }
  }

  async function loadIngredients() {
    setIsLoadingIngredients(true);
    setErrorMessage(null);

    try {
      const response = await fetch(ingredientsUrl);
      const data = (await response.json()) as ApiResponse<Ingredient[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Ingredients could not be loaded.");
      }

      setIngredients(data.result);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Ingredients could not be loaded."
      );
    } finally {
      setIsLoadingIngredients(false);
    }
  }

  async function loadRecipeIngredients(recipeId: number) {
    setIsLoadingRecipeIngredients(true);
    setErrorMessage(null);

    try {
      const response = await fetch(`${recipesUrl}/${recipeId}/ingredients`);
      const data = (await response.json()) as ApiResponse<RecipeIngredient[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Recipe ingredients could not be loaded."
        );
      }

      setRecipeIngredients(data.result);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Recipe ingredients could not be loaded."
      );
    } finally {
      setIsLoadingRecipeIngredients(false);
    }
  }

  async function loadRecipeSteps(recipeId: number) {
    setIsLoadingRecipeSteps(true);
    setErrorMessage(null);

    try {
      const response = await fetch(`${recipesUrl}/${recipeId}/steps`);
      const data = (await response.json()) as ApiResponse<RecipeStep[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe steps could not be loaded.");
      }

      setRecipeSteps(
        [...data.result].sort((first, second) => first.stepNumber - second.stepNumber)
      );
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe steps could not be loaded."
      );
    } finally {
      setIsLoadingRecipeSteps(false);
    }
  }

  async function loadRecipeTools(recipeId: number) {
    setIsLoadingRecipeTools(true);
    setErrorMessage(null);

    try {
      const response = await fetch(`${recipesUrl}/${recipeId}/tools`);
      const data = (await response.json()) as ApiResponse<RecipeTool[]>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe tools could not be loaded.");
      }

      setRecipeTools([...data.result].sort((first, second) => first.name.localeCompare(second.name)));
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe tools could not be loaded."
      );
    } finally {
      setIsLoadingRecipeTools(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const isEditing = editingCategoryId !== null;
      const response = await fetch(
        isEditing ? `${categoriesUrl}/${editingCategoryId}` : categoriesUrl,
        {
          method: isEditing ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            name,
            description
          })
        }
      );

      const data = (await response.json()) as ApiResponse<Category>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Category could not be saved.");
      }

      setMessage(isEditing ? "Category updated successfully." : "Category created successfully.");
      resetForm();
      await loadCategories();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Category could not be saved."
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDelete(categoryId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(`${categoriesUrl}/${categoryId}`, {
        method: "DELETE",
        headers: {
          Authorization: "Bearer " + token
        }
      });

      const data = (await response.json()) as ApiResponse<CategoryDeleteConflictResult | null>;

      if (!data.isSuccess || !response.ok) {
        const recipeCount = data.result?.recipeCount;

        if (response.status === 409 && typeof recipeCount === "number") {
          throw new Error(
            `This category cannot be deleted because ${recipeCount} ${
              recipeCount === 1 ? "recipe is" : "recipes are"
            } using it.`
          );
        }

        throw new Error(data.errorMessages?.[0] ?? "Category could not be deleted.");
      }

      setMessage("Category deleted successfully.");
      setCategories((currentCategories) =>
        currentCategories.filter((category) => category.id !== categoryId)
      );
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Category could not be deleted."
      );
    }
  }

  async function handleIngredientCategorySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingIngredientCategory(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const isEditing = editingIngredientCategoryId !== null;
      const response = await fetch(
        isEditing
          ? `${ingredientCategoriesUrl}/${editingIngredientCategoryId}`
          : ingredientCategoriesUrl,
        {
          method: isEditing ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            name: ingredientCategoryName,
            description: ingredientCategoryDescription
          })
        }
      );

      const data = (await response.json()) as ApiResponse<IngredientCategory>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Ingredient category could not be saved."
        );
      }

      setMessage(
        isEditing
          ? "Ingredient category updated successfully."
          : "Ingredient category created successfully."
      );
      resetIngredientCategoryForm();
      await loadIngredientCategories();
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Ingredient category could not be saved."
      );
    } finally {
      setIsSubmittingIngredientCategory(false);
    }
  }

  async function handleIngredientCategoryDelete(ingredientCategoryId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(
        `${ingredientCategoriesUrl}/${ingredientCategoryId}`,
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
          data.errorMessages?.[0] ?? "Ingredient category could not be deleted."
        );
      }

      setMessage("Ingredient category deleted successfully.");
      await loadIngredientCategories();
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Ingredient category could not be deleted."
      );
    }
  }

  async function handleIngredientSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingIngredient(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const isEditing = editingIngredientId !== null;
      const response = await fetch(
        isEditing ? `${ingredientsUrl}/${editingIngredientId}` : ingredientsUrl,
        {
          method: isEditing ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            name: ingredientName,
            description: ingredientDescription,
            imageUrl: ingredientImageUrl,
            ingredientCategoryId: Number(ingredientCategoryId)
          })
        }
      );

      const data = (await response.json()) as ApiResponse<Ingredient>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Ingredient could not be saved.");
      }

      setMessage(isEditing ? "Ingredient updated successfully." : "Ingredient created successfully.");
      resetIngredientForm();
      await loadIngredients();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Ingredient could not be saved."
      );
    } finally {
      setIsSubmittingIngredient(false);
    }
  }

  async function handleIngredientDelete(ingredientId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(`${ingredientsUrl}/${ingredientId}`, {
        method: "DELETE",
        headers: {
          Authorization: "Bearer " + token
        }
      });

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Ingredient could not be deleted.");
      }

      setMessage("Ingredient deleted successfully.");
      await loadIngredients();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Ingredient could not be deleted."
      );
    }
  }

  async function handleUnitSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingUnit(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const isEditing = editingUnitId !== null;
      const response = await fetch(
        isEditing ? `${unitsUrl}/${editingUnitId}` : unitsUrl,
        {
          method: isEditing ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            name: unitName,
            shortName: unitShortName
          })
        }
      );

      const data = (await response.json()) as ApiResponse<Unit>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Unit could not be saved.");
      }

      setMessage(isEditing ? "Unit updated successfully." : "Unit created successfully.");
      resetUnitForm();
      await loadUnits();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Unit could not be saved."
      );
    } finally {
      setIsSubmittingUnit(false);
    }
  }

  async function handleUnitDelete(unitId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(`${unitsUrl}/${unitId}`, {
        method: "DELETE",
        headers: {
          Authorization: "Bearer " + token
        }
      });

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Unit could not be deleted.");
      }

      setMessage("Unit deleted successfully.");
      await loadUnits();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Unit could not be deleted."
      );
    }
  }

  async function handleRecipeSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingRecipe(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const isEditing = editingRecipeId !== null;
      const response = await fetch(isEditing ? `${recipesUrl}/${editingRecipeId}` : recipesUrl, {
        method: isEditing ? "PUT" : "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer " + token
        },
        body: JSON.stringify({
          title: recipeTitle,
          description: recipeDescription,
          imageUrl: recipeImageUrl,
          preparationTimeMinutes: Number(preparationTimeMinutes),
          cookingTimeMinutes: Number(cookingTimeMinutes),
          servings: Number(servings),
          categoryId: Number(recipeCategoryId)
        })
      });

      const data = (await response.json()) as ApiResponse<Recipe>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe could not be saved.");
      }

      setMessage(isEditing ? "Recipe updated successfully." : "Recipe created successfully.");
      resetRecipeForm();
      await loadRecipes();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe could not be saved."
      );
    } finally {
      setIsSubmittingRecipe(false);
    }
  }

  async function handleRecipeDelete(recipeId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      const response = await fetch(`${recipesUrl}/${recipeId}`, {
        method: "DELETE",
        headers: {
          Authorization: "Bearer " + token
        }
      });

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe could not be deleted.");
      }

      setMessage("Recipe deleted successfully.");

      if (editingRecipeId === recipeId) {
        resetRecipeForm();
      }

      if (selectedRecipe?.id === recipeId) {
        setSelectedRecipe(null);
        setRecipeIngredients([]);
      }

      if (selectedStepsRecipe?.id === recipeId) {
        setSelectedStepsRecipe(null);
        setRecipeSteps([]);
      }

      if (selectedToolsRecipe?.id === recipeId) {
        setSelectedToolsRecipe(null);
        setRecipeTools([]);
      }

      await loadRecipes();
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe could not be deleted."
      );
    }
  }

  function handleManageRecipeIngredients(recipe: Recipe) {
    setMessage(null);
    setErrorMessage(null);
    setSelectedRecipe(recipe);
    setSelectedIngredientId("");
    setSelectedUnitId("");
    setQuantity("");
    loadRecipeIngredients(recipe.id);
  }

  async function handleRecipeIngredientSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingRecipeIngredient(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      if (!selectedRecipe) {
        throw new Error("Select a recipe first.");
      }

      const response = await fetch(`${recipesUrl}/${selectedRecipe.id}/ingredients`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer " + token
        },
        body: JSON.stringify({
          ingredientId: Number(selectedIngredientId),
          unitId: Number(selectedUnitId),
          quantity: Number(quantity)
        })
      });

      const data = (await response.json()) as ApiResponse<RecipeIngredient>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.[0] ?? "Recipe ingredient could not be added."
        );
      }

      setMessage("Recipe ingredient added successfully.");
      setSelectedIngredientId("");
      setSelectedUnitId("");
      setQuantity("");
      await loadRecipeIngredients(selectedRecipe.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Recipe ingredient could not be added."
      );
    } finally {
      setIsSubmittingRecipeIngredient(false);
    }
  }

  async function handleRecipeIngredientDelete(recipeIngredientId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      if (!selectedRecipe) {
        throw new Error("Select a recipe first.");
      }

      const response = await fetch(
        `${recipesUrl}/${selectedRecipe.id}/ingredients/${recipeIngredientId}`,
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
          data.errorMessages?.[0] ?? "Recipe ingredient could not be deleted."
        );
      }

      setMessage("Recipe ingredient deleted successfully.");
      await loadRecipeIngredients(selectedRecipe.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Recipe ingredient could not be deleted."
      );
    }
  }

  function handleManageRecipeSteps(recipe: Recipe) {
    setMessage(null);
    setErrorMessage(null);
    setSelectedStepsRecipe(recipe);
    resetStepForm();
    loadRecipeSteps(recipe.id);
  }

  async function handleRecipeStepSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingRecipeStep(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      if (!selectedStepsRecipe) {
        throw new Error("Select a recipe first.");
      }

      const isEditing = editingStepId !== null;
      const response = await fetch(
        isEditing
          ? `${recipesUrl}/${selectedStepsRecipe.id}/steps/${editingStepId}`
          : `${recipesUrl}/${selectedStepsRecipe.id}/steps`,
        {
          method: isEditing ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            stepNumber: Number(stepNumber),
            instruction
          })
        }
      );

      const data = (await response.json()) as ApiResponse<RecipeStep>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe step could not be saved.");
      }

      setMessage(isEditing ? "Recipe step updated successfully." : "Recipe step added successfully.");
      resetStepForm();
      await loadRecipeSteps(selectedStepsRecipe.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe step could not be saved."
      );
    } finally {
      setIsSubmittingRecipeStep(false);
    }
  }

  async function handleRecipeStepDelete(stepId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      if (!selectedStepsRecipe) {
        throw new Error("Select a recipe first.");
      }

      const response = await fetch(
        `${recipesUrl}/${selectedStepsRecipe.id}/steps/${stepId}`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe step could not be deleted.");
      }

      setMessage("Recipe step deleted successfully.");
      await loadRecipeSteps(selectedStepsRecipe.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe step could not be deleted."
      );
    }
  }

  function handleManageRecipeTools(recipe: Recipe) {
    setMessage(null);
    setErrorMessage(null);
    setSelectedToolsRecipe(recipe);
    resetToolForm();
    loadRecipeTools(recipe.id);
  }

  async function handleRecipeToolSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setErrorMessage(null);
    setIsSubmittingRecipeTool(true);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      if (!selectedToolsRecipe) {
        throw new Error("Select a recipe first.");
      }

      const isEditing = editingToolId !== null;
      const response = await fetch(
        isEditing
          ? `${recipesUrl}/${selectedToolsRecipe.id}/tools/${editingToolId}`
          : `${recipesUrl}/${selectedToolsRecipe.id}/tools`,
        {
          method: isEditing ? "PUT" : "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: "Bearer " + token
          },
          body: JSON.stringify({
            name: toolName
          })
        }
      );

      const data = (await response.json()) as ApiResponse<RecipeTool>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe tool could not be saved.");
      }

      setMessage(isEditing ? "Recipe tool updated successfully." : "Recipe tool added successfully.");
      resetToolForm();
      await loadRecipeTools(selectedToolsRecipe.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe tool could not be saved."
      );
    } finally {
      setIsSubmittingRecipeTool(false);
    }
  }

  async function handleRecipeToolDelete(toolId: number) {
    setMessage(null);
    setErrorMessage(null);

    try {
      const token = getValidToken();

      if (!token) {
        throw new Error("Login token was not found.");
      }

      if (!selectedToolsRecipe) {
        throw new Error("Select a recipe first.");
      }

      const response = await fetch(
        `${recipesUrl}/${selectedToolsRecipe.id}/tools/${toolId}`,
        {
          method: "DELETE",
          headers: {
            Authorization: "Bearer " + token
          }
        }
      );

      const data = (await response.json()) as ApiResponse<unknown>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Recipe tool could not be deleted.");
      }

      setMessage("Recipe tool deleted successfully.");
      await loadRecipeTools(selectedToolsRecipe.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Recipe tool could not be deleted."
      );
    }
  }

  function startEditing(category: Category) {
    setMessage(null);
    setErrorMessage(null);
    setEditingCategoryId(category.id);
    setName(category.name);
    setDescription(category.description ?? "");
  }

  function resetForm() {
    setEditingCategoryId(null);
    setName("");
    setDescription("");
  }

  function startEditingIngredientCategory(ingredientCategory: IngredientCategory) {
    setMessage(null);
    setErrorMessage(null);
    setEditingIngredientCategoryId(ingredientCategory.id);
    setIngredientCategoryName(ingredientCategory.name);
    setIngredientCategoryDescription(ingredientCategory.description ?? "");
  }

  function resetIngredientCategoryForm() {
    setEditingIngredientCategoryId(null);
    setIngredientCategoryName("");
    setIngredientCategoryDescription("");
  }

  function startEditingIngredient(ingredient: Ingredient) {
    setMessage(null);
    setErrorMessage(null);
    setEditingIngredientId(ingredient.id);
    setIngredientName(ingredient.name);
    setIngredientDescription(ingredient.description ?? "");
    setIngredientImageUrl(ingredient.imageUrl ?? "");
    setIngredientCategoryId(ingredient.ingredientCategoryId.toString());
  }

  function resetIngredientForm() {
    setEditingIngredientId(null);
    setIngredientName("");
    setIngredientDescription("");
    setIngredientImageUrl("");
    setIngredientCategoryId("");
  }

  function startEditingUnit(unit: Unit) {
    setMessage(null);
    setErrorMessage(null);
    setEditingUnitId(unit.id);
    setUnitName(unit.name);
    setUnitShortName(unit.shortName);
  }

  function resetUnitForm() {
    setEditingUnitId(null);
    setUnitName("");
    setUnitShortName("");
  }

  function startEditingRecipe(recipe: Recipe) {
    setMessage(null);
    setErrorMessage(null);
    setEditingRecipeId(recipe.id);
    setRecipeTitle(recipe.title);
    setRecipeDescription(recipe.description ?? "");
    setRecipeImageUrl(recipe.imageUrl ?? "");
    setPreparationTimeMinutes(recipe.preparationTimeMinutes.toString());
    setCookingTimeMinutes(recipe.cookingTimeMinutes.toString());
    setServings(recipe.servings.toString());
    setRecipeCategoryId(recipe.categoryId.toString());
  }

  function resetRecipeForm() {
    setEditingRecipeId(null);
    setRecipeTitle("");
    setRecipeDescription("");
    setRecipeImageUrl("");
    setPreparationTimeMinutes("");
    setCookingTimeMinutes("");
    setServings("");
    setRecipeCategoryId("");
  }

  function startEditingStep(step: RecipeStep) {
    setMessage(null);
    setErrorMessage(null);
    setEditingStepId(step.id);
    setStepNumber(step.stepNumber.toString());
    setInstruction(step.instruction);
  }

  function resetStepForm() {
    setEditingStepId(null);
    setStepNumber("");
    setInstruction("");
  }

  function startEditingTool(tool: RecipeTool) {
    setMessage(null);
    setErrorMessage(null);
    setEditingToolId(tool.id);
    setToolName(tool.name);
  }

  function resetToolForm() {
    setEditingToolId(null);
    setToolName("");
  }

  if (isCheckingAccess) {
    return <section className="status-panel">Checking admin access...</section>;
  }

  return (
    <section className="page-section">
      <div className="page-heading">
        <p className="eyebrow">Admin</p>
        <h1 className="page-title">Admin Dashboard</h1>
        <p className="page-description">
          Manage recipe categories and units from one simple admin screen.
        </p>
      </div>

      {message && <div className="status-panel">{message}</div>}
      {errorMessage && <div className="error-panel">{errorMessage}</div>}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">
            {editingCategoryId ? "Update Category" : "Create Category"}
          </h2>

          <form className="mt-5 space-y-4" onSubmit={handleSubmit}>
            <div>
              <label className="form-label" htmlFor="category-name">
                Name
              </label>
              <input
                id="category-name"
                type="text"
                value={name}
                onChange={(event) => setName(event.target.value)}
                required
                className="form-input"
              />
            </div>

            <div>
              <label className="form-label" htmlFor="category-description">
                Description
              </label>
              <textarea
                id="category-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                rows={4}
                className="form-input"
              />
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                type="submit"
                disabled={isSubmitting}
                className="primary-button"
              >
                {isSubmitting
                  ? "Saving..."
                  : editingCategoryId
                    ? "Update Category"
                    : "Create Category"}
              </button>

              {editingCategoryId && (
                <button
                  type="button"
                  onClick={resetForm}
                  className="secondary-button"
                >
                  Cancel
                </button>
              )}
            </div>
          </form>
        </section>

        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">Categories</h2>

          {isLoadingCategories && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Loading categories...
            </div>
          )}

          {!isLoadingCategories && categories.length === 0 && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              No categories found.
            </div>
          )}

          {!isLoadingCategories && categories.length > 0 && (
            <div className="admin-list-scroll">
              {categories.map((category) => (
                <article
                  key={category.id}
                  className="admin-list-item"
                >
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <h3 className="font-semibold text-slate-950">
                        {category.name}
                      </h3>
                      <p className="mt-1 text-sm leading-6 text-slate-600">
                        {category.description ?? "No description provided."}
                      </p>
                    </div>

                    <div className="admin-list-actions">
                      <button
                        type="button"
                        onClick={() => startEditing(category)}
                        className="secondary-button admin-list-button"
                      >
                        Update
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(category.id)}
                        className="danger-button admin-list-button"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">
            {editingIngredientId ? "Update Ingredient" : "Create Ingredient"}
          </h2>

          <form className="mt-5 space-y-4" onSubmit={handleIngredientSubmit}>
            <div>
              <label className="form-label" htmlFor="ingredient-name">
                Name
              </label>
              <input
                id="ingredient-name"
                type="text"
                value={ingredientName}
                onChange={(event) => setIngredientName(event.target.value)}
                required
                className="form-input"
              />
            </div>

            <div>
              <label className="form-label" htmlFor="ingredient-category-id">
                IngredientCategoryId
              </label>
              <select
                id="ingredient-category-id"
                value={ingredientCategoryId}
                onChange={(event) => setIngredientCategoryId(event.target.value)}
                required
                className="form-input"
              >
                <option value="">Select ingredient category</option>
                {ingredientCategories.map((ingredientCategory) => (
                  <option key={ingredientCategory.id} value={ingredientCategory.id}>
                    {ingredientCategory.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="form-label" htmlFor="ingredient-description">
                Description
              </label>
              <textarea
                id="ingredient-description"
                value={ingredientDescription}
                onChange={(event) => setIngredientDescription(event.target.value)}
                rows={3}
                className="form-input"
              />
            </div>

            <div>
              <label className="form-label" htmlFor="ingredient-image-url">
                ImageUrl
              </label>
              <input
                id="ingredient-image-url"
                type="text"
                value={ingredientImageUrl}
                onChange={(event) => setIngredientImageUrl(event.target.value)}
                className="form-input"
              />
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                type="submit"
                disabled={isSubmittingIngredient}
                className="primary-button"
              >
                {isSubmittingIngredient
                  ? "Saving..."
                  : editingIngredientId
                    ? "Update Ingredient"
                    : "Create Ingredient"}
              </button>

              {editingIngredientId && (
                <button
                  type="button"
                  onClick={resetIngredientForm}
                  className="secondary-button"
                >
                  Cancel
                </button>
              )}
            </div>
          </form>
        </section>

        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">Ingredients</h2>

          {isLoadingIngredients && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Loading ingredients...
            </div>
          )}

          {!isLoadingIngredients && ingredients.length === 0 && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              No ingredients found.
            </div>
          )}

          {!isLoadingIngredients && ingredients.length > 0 && (
            <div className="admin-list-scroll">
              {ingredients.map((ingredient) => (
                <article
                  key={ingredient.id}
                  className="admin-list-item"
                >
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <h3 className="font-semibold text-slate-950">
                        {ingredient.name}
                      </h3>
                      <p className="mt-1 text-sm leading-6 text-slate-600">
                        {ingredient.description ?? "No description provided."}
                      </p>
                      <p className="mt-1 text-xs font-semibold uppercase tracking-wide text-emerald-700">
                        {ingredient.ingredientCategoryName ?? "No category"}
                      </p>
                    </div>

                    <div className="admin-list-actions">
                      <button
                        type="button"
                        onClick={() => startEditingIngredient(ingredient)}
                        className="secondary-button admin-list-button"
                      >
                        Update
                      </button>
                      <button
                        type="button"
                        onClick={() => handleIngredientDelete(ingredient.id)}
                        className="danger-button admin-list-button"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">
            {editingIngredientCategoryId
              ? "Update Ingredient Category"
              : "Create Ingredient Category"}
          </h2>

          <form
            className="mt-5 space-y-4"
            onSubmit={handleIngredientCategorySubmit}
          >
            <div>
              <label className="form-label" htmlFor="ingredient-category-name">
                Name
              </label>
              <input
                id="ingredient-category-name"
                type="text"
                value={ingredientCategoryName}
                onChange={(event) => setIngredientCategoryName(event.target.value)}
                required
                className="form-input"
              />
            </div>

            <div>
              <label
                className="form-label"
                htmlFor="ingredient-category-description"
              >
                Description
              </label>
              <textarea
                id="ingredient-category-description"
                value={ingredientCategoryDescription}
                onChange={(event) =>
                  setIngredientCategoryDescription(event.target.value)
                }
                rows={4}
                className="form-input"
              />
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                type="submit"
                disabled={isSubmittingIngredientCategory}
                className="primary-button"
              >
                {isSubmittingIngredientCategory
                  ? "Saving..."
                  : editingIngredientCategoryId
                    ? "Update Ingredient Category"
                    : "Create Ingredient Category"}
              </button>

              {editingIngredientCategoryId && (
                <button
                  type="button"
                  onClick={resetIngredientCategoryForm}
                  className="secondary-button"
                >
                  Cancel
                </button>
              )}
            </div>
          </form>
        </section>

        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">
            Ingredient Categories
          </h2>

          {isLoadingIngredientCategories && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Loading ingredient categories...
            </div>
          )}

          {!isLoadingIngredientCategories && ingredientCategories.length === 0 && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              No ingredient categories found.
            </div>
          )}

          {!isLoadingIngredientCategories && ingredientCategories.length > 0 && (
            <div className="admin-list-scroll">
              {ingredientCategories.map((ingredientCategory) => (
                <article
                  key={ingredientCategory.id}
                  className="admin-list-item"
                >
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <h3 className="font-semibold text-slate-950">
                        {ingredientCategory.name}
                      </h3>
                      <p className="mt-1 text-sm leading-6 text-slate-600">
                        {ingredientCategory.description ?? "No description provided."}
                      </p>
                    </div>

                    <div className="admin-list-actions">
                      <button
                        type="button"
                        onClick={() =>
                          startEditingIngredientCategory(ingredientCategory)
                        }
                        className="secondary-button admin-list-button"
                      >
                        Update
                      </button>
                      <button
                        type="button"
                        onClick={() =>
                          handleIngredientCategoryDelete(ingredientCategory.id)
                        }
                        className="danger-button admin-list-button"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">
            {editingUnitId ? "Update Unit" : "Create Unit"}
          </h2>

          <form className="mt-5 space-y-4" onSubmit={handleUnitSubmit}>
            <div>
              <label className="form-label" htmlFor="unit-name">
                Name
              </label>
              <input
                id="unit-name"
                type="text"
                value={unitName}
                onChange={(event) => setUnitName(event.target.value)}
                required
                className="form-input"
              />
            </div>

            <div>
              <label className="form-label" htmlFor="unit-short-name">
                ShortName
              </label>
              <input
                id="unit-short-name"
                type="text"
                value={unitShortName}
                onChange={(event) => setUnitShortName(event.target.value)}
                required
                className="form-input"
              />
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                type="submit"
                disabled={isSubmittingUnit}
                className="primary-button"
              >
                {isSubmittingUnit
                  ? "Saving..."
                  : editingUnitId
                    ? "Update Unit"
                    : "Create Unit"}
              </button>

              {editingUnitId && (
                <button
                  type="button"
                  onClick={resetUnitForm}
                  className="secondary-button"
                >
                  Cancel
                </button>
              )}
            </div>
          </form>
        </section>

        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">Units</h2>

          {isLoadingUnits && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Loading units...
            </div>
          )}

          {!isLoadingUnits && units.length === 0 && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              No units found.
            </div>
          )}

          {!isLoadingUnits && units.length > 0 && (
            <div className="admin-list-scroll">
              {units.map((unit) => (
                <article
                  key={unit.id}
                  className="admin-list-item"
                >
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <h3 className="font-semibold text-slate-950">
                        {unit.name}
                      </h3>
                      <p className="mt-1 text-sm leading-6 text-slate-600">
                        ShortName: {unit.shortName}
                      </p>
                    </div>

                    <div className="admin-list-actions">
                      <button
                        type="button"
                        onClick={() => startEditingUnit(unit)}
                        className="secondary-button admin-list-button"
                      >
                        Update
                      </button>
                      <button
                        type="button"
                        onClick={() => handleUnitDelete(unit.id)}
                        className="danger-button admin-list-button"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">Admin Recipes</h2>

          <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4">
            <h3 className="font-semibold text-slate-950">
              {editingRecipeId ? "Update Recipe" : "Create Recipe"}
            </h3>
            <form
              className="mt-4 space-y-4"
              onSubmit={handleRecipeSubmit}
            >
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="form-label" htmlFor="recipe-title">
                    Title
                  </label>
                  <input
                    id="recipe-title"
                    type="text"
                    value={recipeTitle}
                    onChange={(event) => setRecipeTitle(event.target.value)}
                    required
                    className="form-input"
                  />
                </div>

                <div>
                  <label className="form-label" htmlFor="recipe-category">
                    CategoryId
                  </label>
                  <select
                    id="recipe-category"
                    value={recipeCategoryId}
                    onChange={(event) => setRecipeCategoryId(event.target.value)}
                    required
                    className="form-input"
                  >
                    <option value="">Select category</option>
                    {categories.map((category) => (
                      <option key={category.id} value={category.id}>
                        {category.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label className="form-label" htmlFor="recipe-description">
                  Description
                </label>
                <textarea
                  id="recipe-description"
                  value={recipeDescription}
                  onChange={(event) => setRecipeDescription(event.target.value)}
                  rows={3}
                  className="form-input"
                />
              </div>

              <div>
                <label className="form-label" htmlFor="recipe-image-url">
                  ImageUrl
                </label>
                <input
                  id="recipe-image-url"
                  type="text"
                  value={recipeImageUrl}
                  onChange={(event) => setRecipeImageUrl(event.target.value)}
                  className="form-input"
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-3">
                <div>
                  <label className="form-label" htmlFor="recipe-prep-time">
                    PreparationTimeMinutes
                  </label>
                  <input
                    id="recipe-prep-time"
                    type="number"
                    min="0"
                    value={preparationTimeMinutes}
                    onChange={(event) =>
                      setPreparationTimeMinutes(event.target.value)
                    }
                    required
                    className="form-input"
                  />
                </div>

                <div>
                  <label className="form-label" htmlFor="recipe-cook-time">
                    CookingTimeMinutes
                  </label>
                  <input
                    id="recipe-cook-time"
                    type="number"
                    min="0"
                    value={cookingTimeMinutes}
                    onChange={(event) =>
                      setCookingTimeMinutes(event.target.value)
                    }
                    required
                    className="form-input"
                  />
                </div>

                <div>
                  <label className="form-label" htmlFor="recipe-servings">
                    Servings
                  </label>
                  <input
                    id="recipe-servings"
                    type="number"
                    min="1"
                    value={servings}
                    onChange={(event) => setServings(event.target.value)}
                    required
                    className="form-input"
                  />
                </div>
              </div>

              <div className="flex flex-wrap gap-3">
                <button
                  type="submit"
                  disabled={isSubmittingRecipe}
                  className="primary-button"
                >
                  {isSubmittingRecipe
                    ? "Saving..."
                    : editingRecipeId
                      ? "Update Recipe"
                      : "Create Recipe"}
                </button>
                {editingRecipeId && (
                  <button
                    type="button"
                    onClick={resetRecipeForm}
                    className="secondary-button"
                  >
                    Cancel
                  </button>
                )}
              </div>
            </form>
          </div>

          {isLoadingRecipes && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Loading recipes...
            </div>
          )}

          {!isLoadingRecipes && recipes.length === 0 && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              No recipes found.
            </div>
          )}

          {!isLoadingRecipes && recipes.length > 0 && (
            <div className="mt-5 space-y-3">
              {recipes.map((recipe) => (
                <article
                  key={recipe.id}
                  className="rounded-lg border border-slate-200 bg-slate-50 p-4"
                >
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <h3 className="font-semibold text-slate-950">
                        {recipe.title}
                      </h3>
                      <p className="mt-1 text-sm leading-6 text-slate-600">
                        {recipe.description ?? "No description provided."}
                      </p>
                    </div>

                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => startEditingRecipe(recipe)}
                        className="secondary-button w-fit"
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        onClick={() => handleRecipeDelete(recipe.id)}
                        className="danger-button w-fit"
                      >
                        Delete
                      </button>
                      <button
                        type="button"
                        onClick={() => handleManageRecipeIngredients(recipe)}
                        className="secondary-button w-fit"
                      >
                        Manage Ingredients
                      </button>
                      <button
                        type="button"
                        onClick={() => handleManageRecipeSteps(recipe)}
                        className="secondary-button w-fit"
                      >
                        Manage Steps
                      </button>
                      <button
                        type="button"
                        onClick={() => handleManageRecipeTools(recipe)}
                        className="secondary-button w-fit"
                      >
                        Manage Tools
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="app-card p-6">
          <h2 className="text-xl font-semibold tracking-normal">
            {selectedRecipe
              ? `Ingredients for ${selectedRecipe.title}`
              : "Recipe Ingredients"}
          </h2>

          {!selectedRecipe && (
            <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
              Choose a recipe and click Manage Ingredients.
            </div>
          )}

          {selectedRecipe && (
            <>
              <form
                className="mt-5 grid gap-4 sm:grid-cols-[1fr_1fr_1fr_auto]"
                onSubmit={handleRecipeIngredientSubmit}
              >
                <div>
                  <label className="form-label" htmlFor="recipe-ingredient">
                    Ingredient
                  </label>
                  <select
                    id="recipe-ingredient"
                    value={selectedIngredientId}
                    onChange={(event) => setSelectedIngredientId(event.target.value)}
                    required
                    className="form-input"
                  >
                    <option value="">Select ingredient</option>
                    {ingredients.map((ingredient) => (
                      <option key={ingredient.id} value={ingredient.id}>
                        {ingredient.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="form-label" htmlFor="recipe-unit">
                    Unit
                  </label>
                  <select
                    id="recipe-unit"
                    value={selectedUnitId}
                    onChange={(event) => setSelectedUnitId(event.target.value)}
                    required
                    className="form-input"
                  >
                    <option value="">Select unit</option>
                    {units.map((unit) => (
                      <option key={unit.id} value={unit.id}>
                        {unit.name} ({unit.shortName})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="form-label" htmlFor="recipe-quantity">
                    Quantity
                  </label>
                  <input
                    id="recipe-quantity"
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={quantity}
                    onChange={(event) => setQuantity(event.target.value)}
                    required
                    className="form-input"
                  />
                </div>

                <div className="flex items-end">
                  <button
                    type="submit"
                    disabled={isSubmittingRecipeIngredient}
                    className="primary-button w-full"
                  >
                    {isSubmittingRecipeIngredient ? "Adding..." : "Add"}
                  </button>
                </div>
              </form>

              {isLoadingRecipeIngredients && (
                <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                  Loading recipe ingredients...
                </div>
              )}

              {!isLoadingRecipeIngredients && recipeIngredients.length === 0 && (
                <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                  No ingredients added to this recipe.
                </div>
              )}

              {!isLoadingRecipeIngredients && recipeIngredients.length > 0 && (
                <div className="mt-5 space-y-3">
                  {recipeIngredients.map((recipeIngredient) => (
                    <article
                      key={recipeIngredient.id}
                      className="rounded-lg border border-slate-200 bg-slate-50 p-4"
                    >
                      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                        <div>
                          <h3 className="font-semibold text-slate-950">
                            {recipeIngredient.ingredientName ?? "Ingredient"}
                          </h3>
                          <p className="mt-1 text-sm text-slate-600">
                            {recipeIngredient.quantity}{" "}
                            {recipeIngredient.unitShortName ?? ""}
                          </p>
                        </div>

                        <button
                          type="button"
                          onClick={() =>
                            handleRecipeIngredientDelete(recipeIngredient.id)
                          }
                          className="danger-button w-fit px-3 py-1 text-xs"
                        >
                          Delete
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </>
          )}
        </section>
      </div>

      <section className="app-card p-6">
        <h2 className="text-xl font-semibold tracking-normal">
          {selectedStepsRecipe
            ? `Steps for ${selectedStepsRecipe.title}`
            : "Recipe Steps"}
        </h2>

        {!selectedStepsRecipe && (
          <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
            Choose a recipe and click Manage Steps.
          </div>
        )}

        {selectedStepsRecipe && (
          <>
            <form
              className="mt-5 grid gap-4 lg:grid-cols-[160px_1fr_auto]"
              onSubmit={handleRecipeStepSubmit}
            >
              <div>
                <label className="form-label" htmlFor="step-number">
                  StepNumber
                </label>
                <input
                  id="step-number"
                  type="number"
                  min="1"
                  value={stepNumber}
                  onChange={(event) => setStepNumber(event.target.value)}
                  required
                  className="form-input"
                />
              </div>

              <div>
                <label className="form-label" htmlFor="step-instruction">
                  Instruction
                </label>
                <input
                  id="step-instruction"
                  type="text"
                  value={instruction}
                  onChange={(event) => setInstruction(event.target.value)}
                  required
                  className="form-input"
                />
              </div>

              <div className="flex items-end gap-2">
                <button
                  type="submit"
                  disabled={isSubmittingRecipeStep}
                  className="primary-button"
                >
                  {isSubmittingRecipeStep
                    ? "Saving..."
                    : editingStepId
                      ? "Update Step"
                      : "Add Step"}
                </button>

                {editingStepId && (
                  <button
                    type="button"
                    onClick={resetStepForm}
                    className="secondary-button"
                  >
                    Cancel
                  </button>
                )}
              </div>
            </form>

            {isLoadingRecipeSteps && (
              <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                Loading recipe steps...
              </div>
            )}

            {!isLoadingRecipeSteps && recipeSteps.length === 0 && (
              <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                No steps added to this recipe.
              </div>
            )}

            {!isLoadingRecipeSteps && recipeSteps.length > 0 && (
              <div className="mt-5 space-y-3">
                {recipeSteps.map((step) => (
                  <article
                    key={step.id}
                    className="rounded-lg border border-slate-200 bg-slate-50 p-4"
                  >
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <h3 className="font-semibold text-slate-950">
                          Step {step.stepNumber}
                        </h3>
                        <p className="mt-1 text-sm leading-6 text-slate-600">
                          {step.instruction}
                        </p>
                      </div>

                      <div className="flex gap-2">
                        <button
                          type="button"
                          onClick={() => startEditingStep(step)}
                          className="secondary-button px-3 py-1 text-xs"
                        >
                          Update
                        </button>
                        <button
                          type="button"
                          onClick={() => handleRecipeStepDelete(step.id)}
                          className="danger-button px-3 py-1 text-xs"
                        >
                          Delete
                        </button>
                      </div>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </>
        )}
      </section>

      <section className="app-card p-6">
        <h2 className="text-xl font-semibold tracking-normal">
          {selectedToolsRecipe
            ? `Tools for ${selectedToolsRecipe.title}`
            : "Recipe Tools"}
        </h2>

        {!selectedToolsRecipe && (
          <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
            Choose a recipe and click Manage Tools.
          </div>
        )}

        {selectedToolsRecipe && (
          <>
            <form
              className="mt-5 grid gap-4 sm:grid-cols-[1fr_auto]"
              onSubmit={handleRecipeToolSubmit}
            >
              <div>
                <label className="form-label" htmlFor="tool-name">
                  Name
                </label>
                <input
                  id="tool-name"
                  type="text"
                  value={toolName}
                  onChange={(event) => setToolName(event.target.value)}
                  required
                  className="form-input"
                />
              </div>

              <div className="flex items-end gap-2">
                <button
                  type="submit"
                  disabled={isSubmittingRecipeTool}
                  className="primary-button"
                >
                  {isSubmittingRecipeTool
                    ? "Saving..."
                    : editingToolId
                      ? "Update Tool"
                      : "Add Tool"}
                </button>

                {editingToolId && (
                  <button
                    type="button"
                    onClick={resetToolForm}
                    className="secondary-button"
                  >
                    Cancel
                  </button>
                )}
              </div>
            </form>

            {isLoadingRecipeTools && (
              <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                Loading recipe tools...
              </div>
            )}

            {!isLoadingRecipeTools && recipeTools.length === 0 && (
              <div className="mt-5 rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                No tools added to this recipe.
              </div>
            )}

            {!isLoadingRecipeTools && recipeTools.length > 0 && (
              <div className="mt-5 space-y-3">
                {recipeTools.map((tool) => (
                  <article
                    key={tool.id}
                    className="rounded-lg border border-slate-200 bg-slate-50 p-4"
                  >
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                      <h3 className="font-semibold text-slate-950">
                        {tool.name}
                      </h3>

                      <div className="flex gap-2">
                        <button
                          type="button"
                          onClick={() => startEditingTool(tool)}
                          className="secondary-button px-3 py-1 text-xs"
                        >
                          Update
                        </button>
                        <button
                          type="button"
                          onClick={() => handleRecipeToolDelete(tool.id)}
                          className="danger-button px-3 py-1 text-xs"
                        >
                          Delete
                        </button>
                      </div>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </>
        )}
      </section>
    </section>
  );
}
