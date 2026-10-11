"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { getRole, getValidToken, setRedirectPath } from "@/lib/auth";

export default function AgentAccess({ admin = false, children }: { admin?: boolean; children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [allowed, setAllowed] = useState(false);

  useEffect(() => {
    if (!getValidToken()) {
      setRedirectPath(pathname);
      router.replace("/login");
      return;
    }
    const role = getRole();
    if (admin ? role !== "Admin" : role !== "User" && role !== "Admin") {
      router.replace("/");
      return;
    }
    // Session storage is available only after mounting.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAllowed(true);
  }, [admin, pathname, router]);

  return allowed ? children : <p className="status-panel" role="status">Checking access...</p>;
}
