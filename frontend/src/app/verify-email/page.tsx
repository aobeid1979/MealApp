"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { FormEvent, Suspense, useState } from "react";

type MessageResult = {
  message: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function VerifyEmailPage() {
  return (
    <Suspense fallback={<p className="status-panel">Loading...</p>}>
      <VerifyEmailForm />
    </Suspense>
  );
}

function VerifyEmailForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const justRegistered = searchParams.get("registered") === "1";
  const [email, setEmail] = useState(searchParams.get("email") ?? "");
  const [code, setCode] = useState("");
  const [message, setMessage] = useState<string | null>(
    justRegistered ? "Account created. We sent a 6-digit code to your email." : null
  );
  const [isError, setIsError] = useState(false);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);

  function showMessage(text: string, error: boolean) {
    setMessage(text);
    setIsError(error);
  }

  async function postToAuth(path: string, body: object) {
    if (!API_URL) {
      throw new Error("The app is not configured: NEXT_PUBLIC_API_URL is missing.");
    }

    let response: Response;

    try {
      response = await fetch(`${API_URL}/api/auth/${path}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
    } catch {
      throw new Error("Unable to connect to the server. Please try again.");
    }

    let data: ApiResponse<MessageResult> | null = null;

    try {
      data = (await response.json()) as ApiResponse<MessageResult>;
    } catch {
      throw new Error("The server returned an invalid response. Please try again.");
    }

    if (!data?.isSuccess || !response.ok) {
      throw new Error(data?.errorMessages?.join(" ") || "Something went wrong. Please try again.");
    }

    return data.result.message;
  }

  async function handleVerify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedEmail = email.trim();
    const trimmedCode = code.trim();

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      showMessage("Enter the email address you registered with.", true);
      return;
    }

    if (!/^\d{6}$/.test(trimmedCode)) {
      showMessage("The code must be 6 digits.", true);
      return;
    }

    setIsVerifying(true);
    setMessage(null);

    try {
      const resultMessage = await postToAuth("verify-email", { email: trimmedEmail, code: trimmedCode });
      showMessage(`${resultMessage} Taking you to the login page...`, false);
      setTimeout(() => router.push("/login"), 2000);
    } catch (error) {
      showMessage(error instanceof Error ? error.message : "Verification failed.", true);
    } finally {
      setIsVerifying(false);
    }
  }

  async function handleResend() {
    const trimmedEmail = email.trim();

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      showMessage("Enter the email address you registered with.", true);
      return;
    }

    setIsResending(true);
    setMessage(null);

    try {
      showMessage(await postToAuth("resend-verification-code", { email: trimmedEmail }), false);
    } catch (error) {
      showMessage(error instanceof Error ? error.message : "Could not send a new code.", true);
    } finally {
      setIsResending(false);
    }
  }

  return (
    <section className="app-card mx-auto max-w-md p-6 sm:p-8">
      <p className="eyebrow">Account</p>
      <h1 className="page-title">Verify your email</h1>
      <p className="mt-3 text-sm leading-6 text-slate-700">
        Enter the 6-digit code we sent to your email. The code is valid for a few minutes.
      </p>

      <form className="mt-6 space-y-4" noValidate onSubmit={handleVerify}>
        <div>
          <label className="form-label" htmlFor="email">
            Email
          </label>
          <input
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            className="form-input"
          />
        </div>

        <div>
          <label className="form-label" htmlFor="code">
            Verification code
          </label>
          <input
            id="code"
            name="code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={6}
            placeholder="123456"
            value={code}
            onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))}
            required
            className="form-input tracking-widest"
          />
        </div>

        <button type="submit" disabled={isVerifying || isResending} className="primary-button w-full">
          {isVerifying ? "Verifying..." : "Verify email"}
        </button>
      </form>

      <button
        type="button"
        onClick={handleResend}
        disabled={isVerifying || isResending}
        className="secondary-button mt-3 w-full"
      >
        {isResending ? "Sending..." : "Send a new code"}
      </button>

      {message && (
        <div
          role={isError ? "alert" : "status"}
          className={
            isError
              ? "error-panel mt-4"
              : "mt-4 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800"
          }
        >
          {message}
        </div>
      )}

      <p className="mt-6 text-sm text-slate-600">
        Already verified?{" "}
        <Link href="/login" className="font-medium text-emerald-700 hover:underline">
          Log in
        </Link>
      </p>
    </section>
  );
}
