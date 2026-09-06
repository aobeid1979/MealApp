"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";

type RegisterResult = {
  message: string;
  email: string;
  firstName: string;
  lastName: string;
  name: string;
  role: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function RegisterPage() {
  const router = useRouter();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setIsError(false);
    setIsSubmitting(true);

    try {
      validateRegisterForm({
        firstName,
        lastName,
        email,
        password,
        confirmPassword
      });

      const response = await fetch(`${API_URL}/api/auth/register`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          email,
          password
        })
      });

      let data: ApiResponse<RegisterResult> | null = null;

      try {
        data = (await response.json()) as ApiResponse<RegisterResult>;
      } catch {
        throw new Error("Unable to connect to the server. Please try again.");
      }

      if (!data.isSuccess || !response.ok) {
        throw new Error(
          data.errorMessages?.length
            ? data.errorMessages.join(" ")
            : "Registration failed."
        );
      }

      setMessage("Registration successful. You can now log in.");
      setTimeout(() => {
        router.push("/login");
      }, 2500);
    } catch (error) {
      setIsError(true);
      setMessage(
        error instanceof TypeError
          ? "Unable to connect to the server. Please try again."
          : error instanceof Error
            ? error.message
            : "Registration failed."
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
          <label className="form-label" htmlFor="first-name">
            First name
          </label>
          <input
            id="first-name"
            type="text"
            value={firstName}
            onChange={(event) => setFirstName(event.target.value)}
            required
            className="form-input"
          />
        </div>

        <div>
          <label className="form-label" htmlFor="last-name">
            Last name
          </label>
          <input
            id="last-name"
            type="text"
            value={lastName}
            onChange={(event) => setLastName(event.target.value)}
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
          <label className="form-label" htmlFor="confirm-password">
            Confirm password
          </label>
          <input
            id="confirm-password"
            type="password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            required
            className="form-input"
          />
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
        <div
          className={
            isError
              ? "error-panel mt-4"
              : "mt-4 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800"
          }
        >
          {message}
        </div>
      )}
    </section>
  );
}

function validateRegisterForm({
  firstName,
  lastName,
  email,
  password,
  confirmPassword
}: {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}) {
  if (!firstName.trim()) {
    throw new Error("First name is required.");
  }

  if (!lastName.trim()) {
    throw new Error("Last name is required.");
  }

  if (!email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    throw new Error("Enter a valid email address.");
  }

  if (!password) {
    throw new Error("Password is required.");
  }

  if (password !== confirmPassword) {
    throw new Error("Confirm password must match password.");
  }
}
