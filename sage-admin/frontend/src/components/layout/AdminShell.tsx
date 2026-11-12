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

  if (loading || !user) return <div className="p-6 text-sm text-zinc-500">Loading...</div>;

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-end gap-4 border-b border-zinc-200 px-6 py-3 text-sm dark:border-zinc-800">
          <span>
            {user.firstName} {user.lastName}
          </span>
          <button
            type="button"
            className="underline"
            onClick={async () => {
              await logout();
              router.replace("/login");
            }}
          >
            Sign out
          </button>
        </header>
        <main className="flex-1 p-6">{children}</main>
      </div>
    </div>
  );
}
