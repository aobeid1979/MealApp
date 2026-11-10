"use client";

import { Fragment, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { AUTH_SESSION_CHANGED, getRole, getValidToken, setRedirectPath } from "@/lib/auth";

export default function AgentAccess({ admin = false, children }: { admin?: boolean; children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [session, setSession] = useState<string | null>(null);

  useEffect(() => {
    function check() {
      const role = getRole();
      const token = getValidToken();
      if (!token) {
        setSession(null);
        setRedirectPath(pathname);
        router.replace("/login");
      } else if (admin ? role !== "Admin" : role !== "User" && role !== "Admin") {
        setSession(null);
        router.replace("/");
      } else setSession(token);
    }
    check();
    window.addEventListener(AUTH_SESSION_CHANGED, check);
    window.addEventListener("focus", check);
    const timer = window.setInterval(check, 1000);
    return () => {
      window.removeEventListener(AUTH_SESSION_CHANGED, check);
      window.removeEventListener("focus", check);
      window.clearInterval(timer);
    };
  }, [admin, pathname, router]);

  // A changed session remounts descendants, discarding the previous user's in-memory data.
  return session ? <Fragment key={session}>{children}</Fragment> : <p className="status-panel" role="status">Checking access...</p>;
}
