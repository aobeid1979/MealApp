"use client";

import Link from "next/link";
import { FormEvent, useEffect, useRef, useState } from "react";
import { getEmail, getName, getValidToken, setRedirectPath } from "@/lib/auth";

type SupportErrors = {
  title?: string;
  description?: string;
  email?: string;
  phone?: string;
  contact?: string;
};

type ApiResponse<T> = {
  isSuccess: boolean;
  result: T;
  errorMessages: string[];
  statusCode: number;
};

type SupportResponse = {
  message: string;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL;

export default function SupportPage() {
  const imageInputRef = useRef<HTMLInputElement>(null);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [userName, setUserName] = useState<string | null>(null);
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [guestName, setGuestName] = useState("");
  const [guestEmail, setGuestEmail] = useState("");
  const [guestPhone, setGuestPhone] = useState("");
  const [image, setImage] = useState<File | null>(null);
  const [errors, setErrors] = useState<SupportErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const token = getValidToken();

    if (!token) {
      return;
    }

    // Auth storage is available only after the page mounts in the browser.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoggedIn(true);
    setUserName(getName());
    setUserEmail(getEmail());
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setApiError(null);

    const validationErrors = validateSupportForm({
      title,
      description,
      isLoggedIn,
      guestEmail,
      guestPhone
    });

    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      const formData = new FormData();
      formData.append("title", title.trim());
      formData.append("description", description.trim());

      if (image) {
        formData.append("image", image);
      }

      if (!isLoggedIn) {
        if (guestName.trim()) {
          formData.append("name", guestName.trim());
        }

        if (guestEmail.trim()) {
          formData.append("email", guestEmail.trim());
        }

        if (guestPhone.trim()) {
          formData.append("phone", guestPhone.trim());
        }
      }

      const token = getValidToken();
      const headers = new Headers();

      if (token) {
        headers.set("Authorization", "Bearer " + token);
      }

      const response = await fetch(`${API_URL}/api/support`, {
        method: "POST",
        headers,
        body: formData
      });

      let data: ApiResponse<SupportResponse> | null = null;

      try {
        data = (await response.json()) as ApiResponse<SupportResponse>;
      } catch {
        throw new Error("Unable to read the server response. Please try again.");
      }

      if (!data.isSuccess || !response.ok) {
        throw new Error(data.errorMessages?.[0] ?? "Support request could not be sent.");
      }

      setTitle("");
      setDescription("");
      setImage(null);

      if (!isLoggedIn) {
        setGuestName("");
        setGuestEmail("");
        setGuestPhone("");
      }

      if (imageInputRef.current) {
        imageInputRef.current.value = "";
      }

      setErrors({});
      setMessage("Your support request has been sent successfully.");
    } catch (error) {
      setApiError(
        error instanceof TypeError
          ? "Unable to connect to the server. Please try again."
          : error instanceof Error
            ? error.message
            : "Support request could not be sent."
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleLoginClick() {
    setRedirectPath("/support");
  }

  return (
    <section className="page-section">
      <div className="page-heading">
        <p className="eyebrow">Support</p>
        <h1 className="page-title">How can we help?</h1>
        <p className="page-description">
          Send a support request, share feedback, or describe a problem you found
          while using MealApp.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.4fr_0.8fr]">
        <form className="app-card space-y-5 p-6 sm:p-8" onSubmit={handleSubmit}>
          {isLoggedIn ? (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">
              You are contacting support as {userName || userEmail || "your account"}.
              {userEmail && (
                <span className="block font-medium">{userEmail}</span>
              )}
            </div>
          ) : (
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
              <p className="text-sm font-semibold text-slate-950">
                Contact information
              </p>
              <p className="mt-2 text-sm leading-6 text-slate-600">
                Add an email or phone number so support can reply to you.
              </p>

              <div className="mt-4 grid gap-4 sm:grid-cols-2">
                <div className="sm:col-span-2">
                  <label className="form-label" htmlFor="support-guest-name">
                    Name
                  </label>
                  <input
                    id="support-guest-name"
                    name="guestName"
                    type="text"
                    value={guestName}
                    onChange={(event) => setGuestName(event.target.value)}
                    className="form-input"
                    placeholder="Your name"
                  />
                </div>

                <div>
                  <label className="form-label" htmlFor="support-guest-email">
                    Email
                  </label>
                  <input
                    id="support-guest-email"
                    name="guestEmail"
                    type="email"
                    value={guestEmail}
                    onChange={(event) => setGuestEmail(event.target.value)}
                    className="form-input"
                    placeholder="you@example.com"
                  />
                  {errors.email && (
                    <p className="mt-2 text-xs font-medium text-red-700">
                      {errors.email}
                    </p>
                  )}
                </div>

                <div>
                  <label className="form-label" htmlFor="support-guest-phone">
                    Phone number
                  </label>
                  <input
                    id="support-guest-phone"
                    name="guestPhone"
                    type="tel"
                    value={guestPhone}
                    onChange={(event) => setGuestPhone(event.target.value)}
                    className="form-input"
                    placeholder="+46..."
                  />
                  {errors.phone && (
                    <p className="mt-2 text-xs font-medium text-red-700">
                      {errors.phone}
                    </p>
                  )}
                </div>
              </div>

              {errors.contact && (
                <p className="mt-3 text-xs font-medium text-red-700">
                  {errors.contact}
                </p>
              )}

              <div className="mt-4 flex flex-wrap items-center gap-3">
                <p className="text-sm text-slate-600">Already have an account?</p>
                <Link
                  href="/login"
                  onClick={handleLoginClick}
                  className="secondary-button"
                >
                  Log in
                </Link>
              </div>
            </div>
          )}

          <div>
            <label className="form-label" htmlFor="support-title">
              Title
            </label>
            <input
              id="support-title"
              name="title"
              type="text"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              className="form-input"
              placeholder="Short summary"
            />
            {errors.title && (
              <p className="mt-2 text-xs font-medium text-red-700">
                {errors.title}
              </p>
            )}
          </div>

          <div>
            <label className="form-label" htmlFor="support-description">
              Description
            </label>
            <textarea
              id="support-description"
              name="description"
              rows={7}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              className="form-input resize-y"
              placeholder="Tell us what happened or what you need help with."
            />
            {errors.description && (
              <p className="mt-2 text-xs font-medium text-red-700">
                {errors.description}
              </p>
            )}
          </div>

          <div>
            <label className="form-label" htmlFor="support-image">
              Image
            </label>
            <input
              id="support-image"
              name="image"
              type="file"
              accept="image/png,image/jpeg,image/webp"
              ref={imageInputRef}
              onChange={(event) => setImage(event.target.files?.[0] ?? null)}
              className="form-input file:mr-4 file:rounded-md file:border-0 file:bg-emerald-50 file:px-3 file:py-2 file:text-sm file:font-semibold file:text-emerald-800 hover:file:bg-emerald-100"
            />
            <p className="mt-2 text-xs text-slate-500">
              Optional screenshot or image attachment.
            </p>
          </div>

          {message && <div className="status-panel">{message}</div>}
          {apiError && <div className="error-panel">{apiError}</div>}

          <button type="submit" disabled={isSubmitting} className="primary-button">
            {isSubmitting ? "Sending..." : "Send"}
          </button>
        </form>

        <aside className="app-card h-fit p-6 sm:p-8">
          <p className="text-sm font-semibold text-slate-950">
            Support address
          </p>
          <a
            href="mailto:meallist.support@gmail.com"
            className="mt-2 inline-flex text-sm font-medium text-emerald-700 transition hover:text-emerald-800"
          >
            meallist.support@gmail.com
          </a>

          <div className="mt-6 border-t border-slate-200 pt-6">
            <p className="text-sm font-semibold text-slate-950">Before you send</p>
            <p className="mt-2 text-sm leading-6 text-slate-600">
              Include the page you were using, what you expected to happen, and
              what happened instead.
            </p>
          </div>

          <Link href="/" className="secondary-button mt-6">
            Back to Home
          </Link>
        </aside>
      </div>
    </section>
  );
}

function validateSupportForm({
  title,
  description,
  isLoggedIn,
  guestEmail,
  guestPhone
}: {
  title: string;
  description: string;
  isLoggedIn: boolean;
  guestEmail: string;
  guestPhone: string;
}) {
  const validationErrors: SupportErrors = {};
  const trimmedEmail = guestEmail.trim();
  const trimmedPhone = guestPhone.trim();

  if (!title.trim()) {
    validationErrors.title = "Title is required.";
  }

  if (!description.trim()) {
    validationErrors.description = "Description is required.";
  }

  if (!isLoggedIn && !trimmedEmail && !trimmedPhone) {
    validationErrors.contact = "Email or phone number is required.";
  }

  if (trimmedEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
    validationErrors.email = "Enter a valid email address.";
  }

  return validationErrors;
}
