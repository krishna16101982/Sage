import type { ReactNode } from "react";
import { AdminShell } from "@/components/layout/AdminShell";

// The admin area is gated by a client-side sign-in check, so pages cannot be prerendered for "instant" navigation.
// This opts the layout out of the static-shell check; pages are covered by instantInsights.validationLevel in next.config.ts.
export const instant = false;

export default function AdminLayout({ children }: { children: ReactNode }) {
  return <AdminShell>{children}</AdminShell>;
}
