// Only the agent pages use this response parser; existing API clients stay unchanged.
export class AgentApiError extends Error {
  constructor(message: string, public status: number, public failedTurn = false) { super(message); }
}

export async function readAgentResponse<T>(response: Response, valid: (value: unknown) => value is T): Promise<T> {
  if (response.status === 401) throw new AgentApiError("Your session has expired. Please log in again.", 401);
  if (response.status === 403) {
    let failedTurn = false;
    try { failedTurn = (await response.json())?.result?.requestState === "Failed"; } catch { /* A non-JSON denial is still a safe permission error. */ }
    throw new AgentApiError("You do not have permission to perform this action.", 403, failedTurn);
  }
  let data;
  try { data = await response.json(); }
  catch { throw new AgentApiError("The server returned an invalid response. Please try again.", response.status); }
  if (!data || typeof data.isSuccess !== "boolean") {
    throw new AgentApiError("The server returned an invalid response. Please try again.", response.status);
  }
  if (!response.ok || !data.isSuccess) {
    const errors = Array.isArray(data.errorMessages) ? data.errorMessages.filter((value: unknown) => typeof value === "string") : [];
    throw new AgentApiError(errors.join(" ") || (response.status === 409
      ? "This proposal has already been decided or can no longer be executed. Refresh the pending list."
      : "The request could not be completed. Please try again."), response.status, data.result?.requestState === "Failed");
  }
  if (!valid(data.result)) throw new AgentApiError("The server returned an invalid response. Please try again.", response.status);
  return data.result;
}

export type ChatReply = {
  conversationId?: string;
  requestId?: string;
  answer: string;
  toolCalls: { tool: string; isSuccess: boolean; code: string }[];
  pendingAction?: { actionId: string; status: string; message: string };
};

export type ConversationSummary = { id: string; title: string; createdAtUtc: string; updatedAtUtc: string };
export type ConversationList = { items: ConversationSummary[]; page: number; hasMore: boolean };
export type SavedMessage = { id: string; role: "User" | "Assistant"; content: string; sequence: number; createdAtUtc: string };
export type ConversationDetails = { conversation: ConversationSummary; messages: SavedMessage[]; hasMore: boolean; nextBeforeSequence: number | null };

export function isConversation(value: unknown): value is ConversationSummary {
  if (!value || typeof value !== "object") return false;
  const v = value as ConversationSummary;
  return typeof v.id === "string" && typeof v.title === "string" && typeof v.createdAtUtc === "string" && typeof v.updatedAtUtc === "string";
}
export function isConversationList(value: unknown): value is ConversationList {
  if (!value || typeof value !== "object") return false;
  const v = value as ConversationList;
  return Array.isArray(v.items) && v.items.every(isConversation) && Number.isInteger(v.page) && typeof v.hasMore === "boolean";
}
export function isConversationDetails(value: unknown): value is ConversationDetails {
  if (!value || typeof value !== "object") return false;
  const v = value as ConversationDetails;
  return isConversation(v.conversation) && typeof v.hasMore === "boolean" && (v.nextBeforeSequence === null || Number.isInteger(v.nextBeforeSequence)) &&
    Array.isArray(v.messages) && v.messages.every(m => m && typeof m.id === "string" && ["User", "Assistant"].includes(m.role) &&
      typeof m.content === "string" && Number.isInteger(m.sequence) && typeof m.createdAtUtc === "string");
}
export function isConversationReply(value: unknown): value is ChatReply {
  return isChatReply(value) && typeof value.conversationId === "string" && typeof value.requestId === "string";
}

export function isChatReply(value: unknown): value is ChatReply {
  if (!value || typeof value !== "object") return false;
  const v = value as ChatReply;
  return typeof v.answer === "string" && Array.isArray(v.toolCalls) && v.toolCalls.every(t =>
    t && typeof t.tool === "string" && typeof t.isSuccess === "boolean" && typeof t.code === "string") &&
    (!v.pendingAction || (typeof v.pendingAction.actionId === "string" && v.pendingAction.status === "Pending"));
}

export type PendingAction = {
  actionId: string; actionType: string; createdAt: string; status: string; recipeId: number | null;
  payload: {
    name: string; description?: string | null; category: string; servings: number;
    preparationTimeMinutes: number; cookingTimeMinutes: number;
    ingredients: { ingredient: string; quantity: number; unit: string }[];
    steps: { stepNumber: number; instruction: string }[]; tools: string[] | null;
  };
};

export function isAction(value: unknown): value is PendingAction {
  if (!value || typeof value !== "object") return false;
  const v = value as PendingAction;
  const p = v.payload;
  return typeof v.actionId === "string" && typeof v.actionType === "string" && typeof v.createdAt === "string" &&
    typeof v.status === "string" && (v.recipeId == null || Number.isInteger(v.recipeId)) && !!p &&
    typeof p.name === "string" && typeof p.category === "string" && (p.description == null || typeof p.description === "string") &&
    [p.servings, p.preparationTimeMinutes, p.cookingTimeMinutes].every(Number.isFinite) &&
    Array.isArray(p.ingredients) && p.ingredients.every(i => i && typeof i.ingredient === "string" && Number.isFinite(i.quantity) && typeof i.unit === "string") &&
    Array.isArray(p.steps) && p.steps.every(s => s && Number.isInteger(s.stepNumber) && typeof s.instruction === "string") &&
    (p.tools == null || (Array.isArray(p.tools) && p.tools.every(t => typeof t === "string")));
}

export function isActionList(value: unknown): value is PendingAction[] {
  return Array.isArray(value) && value.every(isAction);
}
