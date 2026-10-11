type FavoriteRecipe = {
  recipeId: number;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export async function loadFavoriteRecipeIds(token: string) {
  const response = await fetch(`${API_URL}/api/favoriterecipe/my`, {
    headers: {
      Authorization: "Bearer " + token
    }
  });

  const data = (await response.json()) as ApiResponse<FavoriteRecipe[]>;

  if (!data.isSuccess || !response.ok) {
    throw new Error(data.errorMessages?.[0] ?? "Favorite recipes could not be loaded.");
  }

  return new Set(data.result.map((favorite) => favorite.recipeId));
}
