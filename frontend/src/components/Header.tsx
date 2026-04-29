"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

const publicLinks = [
  { href: "/", label: "Home" },
  { href: "/recipes", label: "Recipes" }
];

const protectedLinks = [
  { href: "/meal-plan", label: "Meal Plan" },
  { href: "/shopping-list", label: "Shopping List" }
];

export default function Header() {
  const pathname = usePathname();
  const router = useRouter();
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [userName, setUserName] = useState<string | null>(null);
  const [userRole, setUserRole] = useState<string | null>(null);

  useEffect(() => {
    // localStorage is only available in the browser, so the header syncs after navigation.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoggedIn(Boolean(localStorage.getItem("token")));
    setUserName(localStorage.getItem("name"));
    setUserRole(localStorage.getItem("role"));
  }, [pathname]);

  function handleLogout() {
    localStorage.removeItem("token");
    localStorage.removeItem("email");
    localStorage.removeItem("name");
    localStorage.removeItem("role");
    setIsLoggedIn(false);
    setUserName(null);
    setUserRole(null);
    router.push("/login");
  }

  return (
    <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 shadow-sm backdrop-blur">
      <nav className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
        <Link
          href="/"
          className="text-xl font-bold tracking-normal text-slate-950 transition hover:text-emerald-700"
        >
          MealApp
        </Link>

        <div className="flex flex-wrap items-center gap-2">
          {publicLinks.map((link) => (
            <NavigationLink
              key={link.href}
              href={link.href}
              label={link.label}
              isActive={pathname === link.href}
            />
          ))}

          {isLoggedIn
            ? (
                <>
                  {protectedLinks.map((link) => (
                     <NavigationLink
                       key={link.href}
                       href={link.href}
                       label={link.label}
                       isActive={pathname === link.href}
                     />
                   ))}
                   {userRole === "Admin" && (
                     <NavigationLink
                       href="/admin"
                       label="Admin"
                       isActive={pathname === "/admin"}
                     />
                   )}
                   {userName && (
                     <Link
                       href="/profile"
                       className={
                         pathname === "/profile"
                           ? "rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-800"
                           : "rounded-md bg-slate-100 px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-emerald-50 hover:text-emerald-700"
                       }
                     >
                       {userName}
                     </Link>
                   )}
                   <button
                     type="button"
                     onClick={handleLogout}
                     className="rounded-md border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 transition hover:border-emerald-200 hover:bg-emerald-50 hover:text-emerald-700"
                   >
                     Logout
                   </button>
                </>
              )
            : (
                <>
                  <NavigationLink
                    href="/login"
                    label="Login"
                    isActive={pathname === "/login"}
                  />
                  <NavigationLink
                    href="/register"
                    label="Register"
                    isActive={pathname === "/register"}
                  />
                </>
              )}
        </div>
      </nav>
    </header>
  );
}

function NavigationLink({
  href,
  label,
  isActive
}: {
  href: string;
  label: string;
  isActive: boolean;
}) {
  return (
    <Link
      href={href}
      className={
        isActive
          ? "rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-800"
          : "rounded-md px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-emerald-50 hover:text-emerald-700"
      }
    >
      {label}
    </Link>
  );
}
