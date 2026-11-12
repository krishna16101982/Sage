"use client";

import { useAuth } from "@/contexts/AuthContext";
import { hasPermission } from "@/utils/permissions";

export function usePermission(code: string): boolean {
  const { user } = useAuth();
  return hasPermission(user, code);
}
