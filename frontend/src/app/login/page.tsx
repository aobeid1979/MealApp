"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import {
  clearRedirectPath,
  getRedirectPath,
  saveAuthSession
} from "@/lib/auth";

type LoginResult = {
  message: string;
  email: string;
  name: string;
  role: string | null;
  token: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

// Only follow redirects to pages inside this app, and never back to login/register
// (that would make a successful login look like "nothing happened").
function getSafeRedirectPath(path: string | null) {
  if (!path || !path.startsWith("/") || path.startsWith("//")) {
    return null;
  }

  if (path.startsWith("/login") || path.startsWith("/register")) {
    return null;
  }

  return path;
}

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);

    const trimmedEmail = email.trim();

    // We validate here instead of relying on the browser's built-in popup,
    // which is easy to miss and made the login look like it did nothing.
    if (!trimmedEmail || !password) {
      setMessage("Please enter both your email and your password.");
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      setMessage("Please log in with your email address (for example name@example.com), not a username.");
      return;
    }

    if (!API_URL) {
      setMessage("The app is not configured: NEXT_PUBLIC_API_URL is missing in frontend/.env.local. Restart the frontend after setting it.");
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await fetch(`${API_URL}/api/auth/login`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          email: trimmedEmail,
          password
        })
      });

      let data: ApiResponse<LoginResult> | null = null;

      try {
        data = (await response.json()) as ApiResponse<LoginResult>;
      } catch {
        throw new Error("Unable to connect to the server. Please try again.");
      }

      if (!data || !data.isSuccess || !response.ok) {
        throw new Error(data?.errorMessages?.[0] ?? `Login failed (HTTP ${response.status}).`);
      }

      if (!data.result?.token) {
        throw new Error("Login failed: the server did not return a token.");
      }

      saveAuthSession({
        token: data.result.token,
        email: data.result.email,
        name: data.result.name,
        role: data.result.role ?? ""
      });

      const redirectPath = getSafeRedirectPath(getRedirectPath());
      clearRedirectPath();

      if (redirectPath) {
        router.push(redirectPath);
        return;
      }

      if (data.result.role === "Admin") {
        router.push("/admin");
        return;
      }

      router.push("/");
    } catch (error) {
      setMessage(
        error instanceof TypeError
          ? "Unable to connect to the server. Please try again."
          : error instanceof Error
            ? error.message
            : "Login failed."
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="app-card mx-auto max-w-md p-6 sm:p-8">
      <p className="eyebrow">Account</p>
      <h1 className="page-title">Login</h1>
      <p className="mt-3 text-sm leading-6 text-slate-700">
        Sign in to save recipes to your meal plan.
      </p>

      <form className="mt-6 space-y-4" method="post" noValidate onSubmit={handleSubmit}>
        <div>
          <label className="form-label" htmlFor="email">
            Email
          </label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            className="form-input"
          />
        </div>

        <div>
          <label
            className="form-label"
            htmlFor="password"
          >
            Password
          </label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            className="form-input"
          />
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="primary-button w-full"
        >
          {isSubmitting ? "Logging in..." : "Login"}
        </button>
      </form>

      {message && (
        <div className="error-panel mt-4" role="alert">
          {message}
        </div>
      )}
    </section>
  );
}
