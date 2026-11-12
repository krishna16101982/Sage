import type { CurrentUser } from "@/types/auth";

/** UI-only convenience. The backend independently enforces every permission. */
export function hasPermission(user: CurrentUser | null, code: string): boolean {
  if (!user) return false;
  return user.isSuperAdmin || user.permissions.includes(code);
}
