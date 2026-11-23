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
  requiresEmailVerification?: boolean;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

// Keep this in sync with the registration rule in AuthController.
const MINIMUM_AGE = 18;

export default function RegisterPage() {
  const router = useRouter();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
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
        confirmPassword,
        dateOfBirth
      });

      const response = await fetch(`${API_URL}/api/auth/register`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          email: email.trim(),
          password,
          dateOfBirth
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

      if (data.result?.requiresEmailVerification) {
        // Next step: enter the 6-digit code that was sent to the user's email.
        router.push(`/verify-email?email=${encodeURIComponent(email.trim())}&registered=1`);
        return;
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
          <label className="form-label" htmlFor="date-of-birth">
            Date of birth
          </label>
          <input
            id="date-of-birth"
            type="date"
            value={dateOfBirth}
            max={getTodayString()}
            onChange={(event) => setDateOfBirth(event.target.value)}
            required
            className="form-input"
          />
          <p className="mt-1 text-xs text-slate-500">
            You must be at least {MINIMUM_AGE} years old. Only used to check your age; it is not saved.
          </p>
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
  confirmPassword,
  dateOfBirth
}: {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
  dateOfBirth: string;
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

  if (!dateOfBirth) {
    throw new Error("Date of birth is required.");
  }

  const age = calculateAge(dateOfBirth);

  if (age === null || age > 120) {
    throw new Error("Enter a valid date of birth.");
  }

  if (age < MINIMUM_AGE) {
    throw new Error(`You must be at least ${MINIMUM_AGE} years old to register.`);
  }

  if (!password) {
    throw new Error("Password is required.");
  }

  if (password !== confirmPassword) {
    throw new Error("Confirm password must match password.");
  }
}

// Today's date as "YYYY-MM-DD" in the user's local time (for the date picker's max value).
function getTodayString() {
  return new Date().toISOString().slice(0, 10);
}

// Whole years between a "YYYY-MM-DD" birth date and today; null if the date is invalid or in the future.
function calculateAge(dateOfBirth: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateOfBirth);

  if (!match) {
    return null;
  }

  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const today = new Date();
  const todayYear = today.getUTCFullYear();
  const todayMonth = today.getUTCMonth() + 1;
  const todayDay = today.getUTCDate();

  if (year > todayYear || (year === todayYear && (month > todayMonth || (month === todayMonth && day > todayDay)))) {
    return null;
  }

  let age = todayYear - year;

  if (todayMonth < month || (todayMonth === month && todayDay < day)) {
    age--;
  }

  return age;
}
