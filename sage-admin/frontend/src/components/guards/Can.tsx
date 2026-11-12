"use client";

import type { ReactNode } from "react";
import { usePermission } from "@/hooks/usePermission";

/** Renders children only if the current user holds the permission. Hides buttons/menus (UX only; API enforces). */
export function Can({ permission, children }: { permission: string; children: ReactNode }) {
  return usePermission(permission) ? <>{children}</> : null;
}
