"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { Sidebar } from "@/components/layout/Sidebar";
import { useAuth } from "@/contexts/AuthContext";

/** Client-side route guard for the authenticated area. The API independently enforces every permission. */
export function AdminShell({ children }: { children: ReactNode }) {
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (loading) return;
    if (!user) router.replace("/login");
    else if (user.mustChangePassword && pathname !== "/change-password") router.replace("/change-password");
  }, [user, loading, pathname, router]);

  if (loading || !user) return <div className="page-loading">Loading...</div>;

  return (
    <div className="app-layout">
      <Sidebar />
      <div className="app-main">
        <header className="app-header">
          <span>
            {user.firstName} {user.lastName}
          </span>
          <button
            type="button"
            className="link-button"
            onClick={async () => {
              await logout();
              router.replace("/login");
            }}
          >
            Sign out
          </button>
        </header>
        <main className="app-content">{children}</main>
      </div>
    </div>
  );
}
