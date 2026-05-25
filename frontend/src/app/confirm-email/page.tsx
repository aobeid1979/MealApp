"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";

type ConfirmEmailResult = {
  message: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function ConfirmEmailPage() {
  return (
    <Suspense fallback={<ConfirmEmailCard message="Confirming your email..." />}>
      <ConfirmEmailContent />
    </Suspense>
  );
}

function ConfirmEmailContent() {
  const searchParams = useSearchParams();
  const userId = searchParams.get("userId");
  const token = searchParams.get("token");
  const [message, setMessage] = useState("Confirming your email...");
  const [isError, setIsError] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function confirmEmail() {
      if (!userId || !token) {
        setIsError(true);
        setMessage("Invalid email confirmation link.");
        setIsLoading(false);
        return;
      }

      try {
        const response = await fetch(
          `${API_URL}/api/auth/confirm-email?userId=${encodeURIComponent(
            userId
          )}&token=${encodeURIComponent(token)}`
        );

        const data = (await response.json()) as ApiResponse<ConfirmEmailResult>;

        if (!data.isSuccess || !response.ok) {
          throw new Error(data.errorMessages?.[0] ?? "Email confirmation failed.");
        }

        setMessage(data.result.message ?? "Email confirmed successfully.");
      } catch (error) {
        setIsError(true);
        setMessage(
          error instanceof Error ? error.message : "Email confirmation failed."
        );
      } finally {
        setIsLoading(false);
      }
    }

    confirmEmail();
  }, [token, userId]);

  return (
    <ConfirmEmailCard
      isError={isError}
      isLoading={isLoading}
      message={message}
    />
  );
}

function ConfirmEmailCard({
  isError = false,
  isLoading = false,
  message
}: {
  isError?: boolean;
  isLoading?: boolean;
  message: string;
}) {
  return (
    <section className="app-card mx-auto max-w-md p-6 text-center sm:p-8">
      <p className="eyebrow">Account</p>
      <h1 className="page-title">Confirm Email</h1>
      <p
        className={
          isError
            ? "mt-4 text-sm leading-6 text-red-700"
            : "mt-4 text-sm leading-6 text-slate-700"
        }
      >
        {message}
      </p>

      {!isLoading && !isError && (
        <Link className="primary-button mt-6 inline-flex" href="/login">
          Go to login
        </Link>
      )}
    </section>
  );
}
