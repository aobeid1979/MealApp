"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";

type RegisterResult = {
  message: string;
  email: string;
  role: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

export default function RegisterPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("User");
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setIsError(false);
    setIsSubmitting(true);

    try {
      const response = await fetch("http://localhost:5280/api/auth/register", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          name,
          email,
          password,
          role
        })
      });

      const data = (await response.json()) as ApiResponse<RegisterResult>;

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Registration failed.");
      }

      setMessage("Registration successful. Redirecting to login...");
      setTimeout(() => {
        router.push("/login");
      }, 1000);
    } catch (error) {
      setIsError(true);
      setMessage(
        error instanceof Error ? error.message : "Registration failed."
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="app-card mx-auto max-w-md p-6 sm:p-8">
      <p className="eyebrow">Account</p>
      <h1 className="page-title">Register</h1>
      <p className="mt-3 text-sm leading-6 text-slate-700">
        Create an account so you can save recipes and meal plans.
      </p>

      <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
        <div>
          <label className="form-label" htmlFor="name">
            Name
          </label>
          <input
            id="name"
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            className="form-input"
          />
        </div>

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
          <label className="form-label" htmlFor="password">
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

        <div>
          <label className="form-label" htmlFor="role">
            Role
          </label>
          <select
            id="role"
            value={role}
            onChange={(event) => setRole(event.target.value)}
            className="form-input"
          >
            <option value="User">User</option>
            <option value="Admin">Admin</option>
          </select>
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="primary-button w-full"
        >
          {isSubmitting ? "Creating account..." : "Register"}
        </button>
      </form>

      {message && (
        <p
          className={
            isError
              ? "mt-4 text-sm text-red-700"
              : "mt-4 text-sm text-slate-700"
          }
        >
          {message}
        </p>
      )}
    </section>
  );
}
