"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import AgentAccess from "@/components/AgentAccess";
import { clearAuthSession, getValidToken, setRedirectPath } from "@/lib/auth";
import { AgentApiError, isAction, isActionList, PendingAction, readAgentResponse } from "@/lib/agent-response";

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function AgentActionsPage() {
  return <AgentAccess admin><PendingActions /></AgentAccess>;
}

function PendingActions() {
  const router = useRouter();
  const [actions, setActions] = useState<PendingAction[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyIds, setBusyIds] = useState<string[]>([]);
  const locks = useRef(new Set<string>());
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PendingAction | null>(null);

  const handleError = useCallback((caught: unknown) => {
    if (caught instanceof AgentApiError && caught.status === 401) {
      clearAuthSession(); setRedirectPath("/admin/agent-actions"); router.replace("/login");
    }
    setError(caught instanceof AgentApiError ? caught.message : "Unable to connect to the server. Please try again.");
  }, [router]);

  const tokenForRequest = useCallback(() => {
    const token = getValidToken();
    if (!token) throw new AgentApiError("Your session has expired. Please log in again.", 401);
    if (!API_URL) throw new AgentApiError("The API URL is not configured.", 503);
    return token;
  }, []);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    try {
      const token = tokenForRequest();
      const response = await fetch(`${API_URL}/api/agent-actions/pending`, { headers: { Authorization: `Bearer ${token}` }, signal });
      const pending = await readAgentResponse(response, isActionList);
      if (!signal?.aborted) setActions(pending);
    } catch (caught) { if (!signal?.aborted) handleError(caught); }
    finally { if (!signal?.aborted) setLoading(false); }
  }, [handleError, tokenForRequest]);

  useEffect(() => {
    const controller = new AbortController();
    // Start the initial authenticated fetch; later state updates follow the network response.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function decide(id: string, decision: "approve" | "reject") {
    if (locks.current.has(id)) return;
    locks.current.add(id);
    setBusyIds(Array.from(locks.current)); setError(null); setResult(null);
    try {
      const token = tokenForRequest();
      const response = await fetch(`${API_URL}/api/agent-actions/${encodeURIComponent(id)}/${decision}`, {
        method: "POST", headers: { Authorization: `Bearer ${token}` }
      });
      const outcome = await readAgentResponse(response, isAction);
      if (outcome.status !== (decision === "approve" ? "Executed" : "Rejected")) {
        throw new AgentApiError("The action did not complete successfully. Refresh the pending list.", 409);
      }
      setResult(outcome);
      setActions(previous => previous.filter(action => action.actionId !== id));
      await load();
    } catch (caught) {
      handleError(caught);
      // A conflict or failed execution can already have transitioned the action.
      if (caught instanceof AgentApiError && caught.status !== 401 && caught.status !== 403) await load();
    } finally { locks.current.delete(id); setBusyIds(Array.from(locks.current)); }
  }

  return <section className="page-section">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h1 className="page-title">Pending agent proposals</h1>
      <div className="flex gap-2"><Link href="/assistant" className="secondary-button">Assistant</Link><button className="secondary-button" disabled={loading || busyIds.length > 0} onClick={() => { setError(null); void load(); }}>Refresh</button></div>
    </div>
    {error && <p role="alert" className="error-panel">{error}</p>}
    {result && <div role="status" className="status-panel">
      {result.status === "Executed" ? <><p>Approved and published. Created Recipe ID: {result.recipeId}</p>{result.recipeId && <Link href={`/recipes/${result.recipeId}`} className="secondary-button mt-3">View recipe</Link>}</> : <p>Proposal rejected. No recipe was published.</p>}
    </div>}
    {loading && <p className="status-panel" role="status">Loading proposals...</p>}
    {!loading && !error && actions.length === 0 && <p className="status-panel">No pending proposals.</p>}
    {actions.map(action => <article key={action.actionId} className="app-card space-y-5 p-5">
      <div><p className="text-sm text-slate-600 break-words">{action.actionType} · {action.actionId.slice(0, 8)} · {action.status}</p><h2 className="mt-2 text-xl font-bold break-words">{action.payload.name}</h2><p className="mt-1 text-sm text-slate-600">{new Date(action.createdAt).toLocaleString()}</p></div>
      {action.payload.description && <p className="whitespace-pre-wrap break-words">{action.payload.description}</p>}
      <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">{[
        ["Category", action.payload.category], ["Servings", action.payload.servings],
        ["Preparation", `${action.payload.preparationTimeMinutes} min`], ["Cooking", `${action.payload.cookingTimeMinutes} min`]
      ].map(([label, value]) => <div key={label}><dt className="text-slate-600">{label}</dt><dd className="font-semibold break-words">{value}</dd></div>)}</dl>
      <div className="grid gap-5 sm:grid-cols-2">
        <div><h3 className="font-semibold">Ingredients</h3><ul className="mt-2 space-y-2 text-sm">{action.payload.ingredients.map((item, i) => <li key={i} className="break-words">{item.ingredient}: {item.quantity} {item.unit}</li>)}</ul></div>
        <div><h3 className="font-semibold">Tools</h3><ul className="mt-2 space-y-2 text-sm">{action.payload.tools?.map((tool, i) => <li key={i} className="break-words">{tool}</li>)}</ul>{!action.payload.tools?.length && <p className="mt-2 text-sm text-slate-600">No tools specified.</p>}</div>
      </div>
      <div><h3 className="font-semibold">Steps</h3><ol className="mt-2 space-y-2 text-sm">{[...action.payload.steps].sort((a, b) => a.stepNumber - b.stepNumber).map((step, i) => <li key={i} className="whitespace-pre-wrap break-words">{step.stepNumber}. {step.instruction}</li>)}</ol></div>
      <div className="flex flex-wrap gap-3"><button className="primary-button" disabled={busyIds.includes(action.actionId) || loading} onClick={() => void decide(action.actionId, "approve")}>{busyIds.includes(action.actionId) ? "Processing..." : "Approve"}</button><button className="danger-button" disabled={busyIds.includes(action.actionId) || loading} onClick={() => void decide(action.actionId, "reject")}>Reject</button></div>
    </article>)}
  </section>;
}
