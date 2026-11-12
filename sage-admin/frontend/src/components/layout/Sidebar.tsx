"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { hasPermission } from "@/utils/permissions";

/** Navigation shell. Each item names the permission that gates it; items come from this list
 *  only for routing, visibility is decided by the permissions the API returns for the user. */
const ITEMS = [
  { href: "/dashboard", label: "Dashboard", permission: "Dashboard.View" },
  { href: "/users", label: "Admin Users", permission: "AdminUser.View" },
  { href: "/groups", label: "Groups", permission: "Group.View" },
  { href: "/permissions", label: "Permissions", permission: "Permission.View" },
  { href: "/products", label: "Products", permission: "Product.View" },
  { href: "/sales-users", label: "Sales Users", permission: "SalesUser.View" },
  { href: "/audit-logs", label: "Audit Logs", permission: "AuditLog.View" },
  { href: "/settings", label: "Settings", permission: "Setting.View" },
];

export function Sidebar() {
  const { user } = useAuth();
  const pathname = usePathname();
  return (
    <nav aria-label="Main" className="sidebar">
      <div className="sidebar-title">Sage Admin</div>
      <ul className="space-y-1">
        {ITEMS.filter((i) => hasPermission(user, i.permission)).map((i) => (
          <li key={i.href}>
            <Link className="sidebar-link" href={i.href} aria-current={pathname.startsWith(i.href) ? "page" : undefined}>
              {i.label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
