"use client";

import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { hasPermission } from "@/utils/permissions";

/** Navigation shell. Each item names the permission that gates it; items come from this list
 *  only for routing, visibility is decided by the permissions the API returns for the user. */
const ITEMS = [
  { href: "/dashboard", label: "Dashboard", permission: "Dashboard.View" },
  { href: "/users", label: "Admin Users", permission: "AdminUser.View" },
  { href: "/groups", label: "Groups", permission: "Group.View" },
  { href: "/permissions", label: "Permissions", permission: "Permission.View" },
  { href: "/audit-logs", label: "Audit Logs", permission: "AuditLog.View" },
  { href: "/settings", label: "Settings", permission: "Setting.View" },
];

export function Sidebar() {
  const { user } = useAuth();
  return (
    <nav aria-label="Main" className="w-56 shrink-0 border-r border-zinc-200 p-4 dark:border-zinc-800">
      <div className="mb-6 text-lg font-semibold">Sage Admin</div>
      <ul className="space-y-1">
        {ITEMS.filter((i) => hasPermission(user, i.permission)).map((i) => (
          <li key={i.href}>
            <Link className="block rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800" href={i.href}>
              {i.label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
