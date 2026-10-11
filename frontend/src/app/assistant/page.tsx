"use client";

import Link from "next/link";
import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import AgentAccess from "@/components/AgentAccess";
import { clearAuthSession, getRole, getValidToken, setRedirectPath } from "@/lib/auth";
import { AgentApiError, ChatReply, isChatReply, readAgentResponse } from "@/lib/agent-response";

const API_URL = process.env.NEXT_PUBLIC_API_URL;
type Message = { text: string; reply?: ChatReply; user: boolean };

export default function AssistantPage() {
  return <AgentAccess><Conversation /></AgentAccess>;
}

function Conversation() {
  const router = useRouter();
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const busy = useRef(false);
  const activeRequest = useRef<AbortController | null>(null);
  useEffect(() => () => activeRequest.current?.abort(), []);

  async function send(event: FormEvent) {
    event.preventDefault();
    if (busy.current || !input.trim()) return;
    setError(null);
    const token = getValidToken();
    if (!token) { setRedirectPath("/assistant"); router.replace("/login"); return; }
    const text = input.trim();
    busy.current = true;
    setSending(true);
    setMessages(previous => [...previous, { text, user: true }]);
    const controller = new AbortController();
    activeRequest.current = controller;
    const timeout = window.setTimeout(() => controller.abort(), 610000);
    try {
      if (!API_URL) throw new Error("The API URL is not configured.");
      const response = await fetch(`${API_URL}/api/agent/chat`, {
        method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
        body: JSON.stringify({ message: text }), signal: controller.signal
      });
      const reply = await readAgentResponse(response, isChatReply);
      setMessages(previous => [...previous, { text: reply.answer, reply, user: false }]);
      setInput("");
    } catch (caught) {
      if (caught instanceof AgentApiError && caught.status === 401) {
        clearAuthSession(); setRedirectPath("/assistant"); router.replace("/login");
      }
      setError(caught instanceof AgentApiError ? caught.message : controller.signal.aborted
        ? "The assistant request timed out or was cancelled. Please try again."
        : !API_URL ? "The API URL is not configured." : "Unable to connect to the assistant. Please try again.");
    } finally {
      window.clearTimeout(timeout);
      activeRequest.current = null;
      busy.current = false;
      setSending(false);
    }
  }

  return <section className="page-section">
    <div className="page-heading"><h1 className="page-title">MealApp Assistant</h1></div>
    <div className="min-h-64 space-y-5" role="log" aria-label="Conversation" aria-live="polite">
      {messages.length === 0 && <p className="status-panel">What would you like to cook?</p>}
      {messages.map((message, index) => <article key={index} className={`rounded-lg border p-4 ${message.user ? "ml-4 border-emerald-200 bg-emerald-50" : "mr-4 border-slate-200 bg-white"}`}>
        <p className="mb-2 text-sm font-semibold">{message.user ? "You" : "Assistant"}</p>
        <p className="whitespace-pre-wrap break-words">{message.text}</p>
        {message.reply?.pendingAction && <div className="mt-4 border-t border-amber-200 pt-3 text-sm text-amber-900">
          <p className="font-semibold">Pending approval</p>
          <p>The recipe proposal is awaiting approval and has not been published.</p>
          {getRole() === "Admin" && <Link href="/admin/agent-actions" className="secondary-button mt-3">Review proposals</Link>}
        </div>}
        {!!message.reply?.toolCalls.length && <details className="mt-4 text-sm text-slate-600">
          <summary className="cursor-pointer font-medium">Agent activity</summary>
          <ul className="mt-2 space-y-1">{message.reply.toolCalls.map((tool, i) => <li key={i} className="break-words">{tool.tool} · {tool.isSuccess ? "Success" : "Failed"} · {tool.code}</li>)}</ul>
        </details>}
      </article>)}
      {sending && <p role="status" className="text-sm text-slate-600">Thinking...</p>}
    </div>
    {error && <p role="alert" className="error-panel">{error}</p>}
    <form onSubmit={send} className="space-y-3">
      <label className="form-label" htmlFor="agent-message">Message</label>
      <textarea id="agent-message" className="form-input" rows={3} maxLength={2000} required value={input} onChange={e => setInput(e.target.value)} disabled={sending} />
      <button className="primary-button" disabled={sending || !input.trim()}>{sending ? "Thinking..." : "Send"}</button>
    </form>
  </section>;
}
