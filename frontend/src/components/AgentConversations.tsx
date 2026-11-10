"use client";

import Link from "next/link";
import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { clearAuthSession, getRole, getValidToken, setRedirectPath } from "@/lib/auth";
import { createTurnRequestId } from "@/lib/agent-request";
import {
  AgentApiError, ChatReply, ConversationDetails, ConversationList, ConversationSummary,
  isConversation, isConversationDetails, isConversationList, isConversationReply, readAgentResponse
} from "@/lib/agent-response";

const API_URL = process.env.NEXT_PUBLIC_API_URL;
type Attempt = { conversationId: string; requestId: string; message: string };

export default function AgentConversations() {
  const router = useRouter();
  const [list, setList] = useState<ConversationList>({ items: [], page: 1, hasMore: false });
  const [details, setDetails] = useState<ConversationDetails | null>(null);
  const [input, setInput] = useState("");
  const [working, setWorking] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [failedTurn, setFailedTurn] = useState(false);
  const [retryPending, setRetryPending] = useState(false);
  const [liveReply, setLiveReply] = useState<ChatReply | null>(null);
  const busy = useRef(false);
  const attempt = useRef<Attempt | null>(null);
  const lifetime = useRef<AbortController | null>(null);

  const request = useCallback(async <T,>(path: string, valid: (value: unknown) => value is T, init: RequestInit = {}) => {
    const token = getValidToken();
    if (!token) throw new AgentApiError("Your session has expired. Please log in again.", 401);
    if (!API_URL) throw new Error("API URL missing");
    const response = await fetch(`${API_URL}/api/agent/${path}`, {
      ...init, cache: "no-store", signal: lifetime.current?.signal,
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}`, ...init.headers }
    });
    const result = await readAgentResponse(response, valid);
    if (lifetime.current?.signal.aborted || getValidToken() !== token) throw new Error("Session changed");
    return result;
  }, []);

  const showError = useCallback((caught: unknown) => {
    if (lifetime.current?.signal.aborted) return;
    if (caught instanceof AgentApiError && caught.status === 401) {
      clearAuthSession(); setRedirectPath("/assistant"); router.replace("/login");
      return;
    }
    setFailedTurn(caught instanceof AgentApiError && caught.failedTurn);
    setError(caught instanceof AgentApiError ? caught.message : !API_URL ? "The API URL is not configured."
      : "Unable to connect to the assistant. Retry the same message to check its status.");
  }, [router]);

  const open = useCallback(async (id: string) => {
    const result = await request(`conversations/${id}`, isConversationDetails);
    setDetails(result); setInput(""); setLiveReply(null); setFailedTurn(false); setRetryPending(false); attempt.current = null;
    window.history.replaceState(null, "", `/assistant?conversationId=${encodeURIComponent(id)}`);
  }, [request]);

  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    busy.current = true;
    void (async () => {
      try {
        const result = await request("conversations", isConversationList);
        if (controller.signal.aborted) return;
        setList(result);
        const selected = new URLSearchParams(window.location.search).get("conversationId");
        if (selected || result.items[0]) await open(selected || result.items[0].id);
      } catch (caught) { if (!controller.signal.aborted) showError(caught); }
      finally { if (!controller.signal.aborted) { busy.current = false; setWorking(false); } }
    })();
    return () => controller.abort();
  }, [open, request, showError]);

  async function action(run: () => Promise<void>) {
    if (busy.current) return;
    busy.current = true; setWorking(true); setError(null);
    try { await run(); } catch (caught) { showError(caught); }
    finally { busy.current = false; setWorking(false); }
  }

  function create() {
    void action(async () => {
      const created = await request("conversations", isConversation, { method: "POST", body: "{}" });
      await open(created.id);
      setList(await request("conversations", isConversationList));
    });
  }

  function remove(conversation: ConversationSummary) {
    if (!window.confirm(`Delete "${conversation.title}" and its messages?`)) return;
    void action(async () => {
      await request(`conversations/${conversation.id}`, (v): v is { message: string } =>
        !!v && typeof v === "object" && "message" in v && typeof v.message === "string", { method: "DELETE" });
      if (details?.conversation.id === conversation.id) {
        setDetails(null); setInput(""); setLiveReply(null); attempt.current = null; setFailedTurn(false); setRetryPending(false);
        window.history.replaceState(null, "", "/assistant");
      }
      setList(await request(`conversations?page=${list.page}`, isConversationList));
    });
  }

  async function send(event: FormEvent) {
    event.preventDefault();
    if (busy.current || !details || !input.trim() || failedTurn) return;
    const id = details.conversation.id;
    if (attempt.current && (attempt.current.conversationId !== id || attempt.current.message !== input.trim())) {
      setError("Retry the original message or reload before starting a different turn."); return;
    }
    setSending(true);
    await action(async () => {
      attempt.current ??= { conversationId: id, requestId: createTurnRequestId(), message: input.trim() };
      const pending = attempt.current;
      setRetryPending(true);
      const reply = await request("chat", isConversationReply, { method: "POST", body: JSON.stringify(pending) });
      if (reply.conversationId !== id || reply.requestId !== pending.requestId) throw new Error("Unexpected turn response");
      const saved = await request(`conversations/${id}`, isConversationDetails);
      setDetails(saved); setLiveReply(reply); setInput(""); attempt.current = null; setFailedTurn(false); setRetryPending(false);
      setList(await request("conversations", isConversationList));
    });
    setSending(false);
  }

  return <section className="page-section">
    <div className="page-heading"><h1 className="page-title">MealApp Assistant</h1></div>
    <div className="grid gap-6 md:grid-cols-[240px_minmax(0,1fr)]">
      <aside className="space-y-3 border-b border-slate-200 pb-4 md:border-b-0 md:border-r md:pr-4">
        <button className="primary-button w-full" onClick={create} disabled={working}>New Chat</button>
        <nav aria-label="Conversations" className="max-h-96 space-y-1 overflow-y-auto">
          {list.items.map(c => <div key={c.id} className="flex items-start gap-1">
            <button className={`min-w-0 flex-1 break-words rounded p-2 text-left text-sm hover:bg-emerald-50 disabled:opacity-50 ${details?.conversation.id === c.id ? "bg-emerald-50 font-semibold text-emerald-800" : ""}`}
              aria-current={details?.conversation.id === c.id ? "true" : undefined} disabled={working} onClick={() => void action(() => open(c.id))}>{c.title}</button>
            <button className="shrink-0 p-2 text-sm text-red-700 hover:bg-red-50 disabled:opacity-50" aria-label={`Delete ${c.title}`} disabled={working} onClick={() => remove(c)}>Delete</button>
          </div>)}
        </nav>
        {!working && !list.items.length && <p className="text-sm text-slate-600">No conversations yet.</p>}
        <div className="flex gap-2">
          <button className="secondary-button" disabled={working || list.page === 1} onClick={() => void action(async () => setList(await request(`conversations?page=${list.page - 1}`, isConversationList)))}>Previous</button>
          <button className="secondary-button" disabled={working || !list.hasMore} onClick={() => void action(async () => setList(await request(`conversations?page=${list.page + 1}`, isConversationList)))}>Next</button>
        </div>
      </aside>
      <div className="min-w-0 space-y-4">
        {working && <p role="status" className="status-panel">{sending ? "Thinking..." : "Loading conversations..."}</p>}
        {error && <p role="alert" className="error-panel">{error}</p>}
        {!details && !working && <p className="status-panel">Start a new chat or choose a conversation.</p>}
        {details && <>
          <h2 className="text-lg font-semibold break-words">{details.conversation.title}</h2>
          {details.hasMore && <button className="secondary-button" disabled={working} onClick={() => void action(async () => {
            const older = await request(`conversations/${details.conversation.id}?beforeSequence=${details.nextBeforeSequence}`, isConversationDetails);
            setDetails({ ...older, messages: [...older.messages, ...details.messages] });
          })}>Load older messages</button>}
          <div className="min-h-64 space-y-5" role="log" aria-label="Conversation" aria-live="polite">
            {!details.messages.length && <p className="status-panel">What would you like to cook?</p>}
            {details.messages.map((m, index) => <article key={m.id} className={`rounded-lg border p-4 ${m.role === "User" ? "ml-4 border-emerald-200 bg-emerald-50" : "mr-4 border-slate-200 bg-white"}`}>
              <div className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <span className="font-semibold">{m.role === "User" ? "You" : "Assistant"}</span>
                <time className="text-xs text-slate-500" dateTime={m.createdAtUtc}>{new Date(m.createdAtUtc).toLocaleString()}</time>
              </div>
              <p className="whitespace-pre-wrap break-words">{m.content}</p>
              {index === details.messages.length - 1 && liveReply && <>
                {liveReply.pendingAction && getRole() === "Admin" && <Link href="/admin/agent-actions" className="secondary-button mt-3">Review proposal status</Link>}
                {!!liveReply.toolCalls.length && <details className="mt-4 text-sm text-slate-600">
                  <summary className="cursor-pointer font-medium">Agent activity</summary>
                  <ul className="mt-2 space-y-1">{liveReply.toolCalls.map((t, i) => <li key={i} className="break-words">{t.tool} · {t.isSuccess ? "Success" : "Failed"} · {t.code}</li>)}</ul>
                </details>}
              </>}
            </article>)}
          </div>
          {failedTurn && <button className="secondary-button" disabled={working} onClick={() => { attempt.current = null; setFailedTurn(false); setRetryPending(false); setError(null); }}>Start a new attempt</button>}
          <form onSubmit={send} className="space-y-3">
            <label className="form-label" htmlFor="agent-message">Message</label>
            <textarea id="agent-message" className="form-input" rows={3} maxLength={2000} required value={input}
              onChange={e => setInput(e.target.value)} disabled={working || (retryPending && !failedTurn)}
              onKeyDown={e => {
                // Enter sends the message; Shift+Enter adds a new line. Skip while an IME is composing text.
                if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
                  e.preventDefault();
                  e.currentTarget.form?.requestSubmit();
                }
              }} />
            <button className="primary-button" disabled={working || failedTurn || !input.trim()}>{sending ? "Thinking..." : retryPending ? "Retry same message" : "Send"}</button>
          </form>
        </>}
      </div>
    </div>
  </section>;
}
