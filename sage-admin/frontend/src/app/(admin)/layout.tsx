import type { ReactNode } from "react";
import { AdminShell } from "@/components/layout/AdminShell";

// The admin area is gated by a client-side sign-in check, so pages cannot be prerendered for "instant" navigation.
// This only silences the Next.js dev-overlay insight for these segments; it does not change behavior.
export const instant = false;

export default function AdminLayout({ children }: { children: ReactNode }) {
  return <AdminShell>{children}</AdminShell>;
}
