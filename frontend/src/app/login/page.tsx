"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";

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

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setIsSubmitting(true);

    try {
      const response = await fetch(`${API_URL}/api/auth/login`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          email,
          password
        })
      });

      const data = (await response.json()) as ApiResponse<LoginResult>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Login failed.");
      }

      localStorage.setItem("token", data.result.token);
      localStorage.setItem("email", data.result.email);
      localStorage.setItem("name", data.result.name);
      localStorage.setItem("role", data.result.role ?? "");

      const redirectPath = localStorage.getItem("redirectPath");
      localStorage.removeItem("redirectPath");

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
      setMessage(error instanceof Error ? error.message : "Login failed.");
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

      <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
        <div>
          <label className="form-label" htmlFor="email">
            Email
          </label>
          <input
            id="email"
            type="email"
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
            type="password"
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

      {message && <p className="mt-4 text-sm text-slate-700">{message}</p>}
    </section>
  );
}
