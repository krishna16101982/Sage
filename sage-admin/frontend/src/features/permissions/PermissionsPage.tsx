"use client";

import { useCallback, useEffect, useState } from "react";
import { Alert } from "@/components/ui/Form";
import { getModules } from "@/services/permissionService";
import { ApiError } from "@/types/api";
import type { ModuleItemDto, PermissionItem } from "@/types/permission";
import { ModuleFormDialog } from "./ModuleFormDialog";
import { ModulesTab } from "./ModulesTab";
import { PermissionFormDialog } from "./PermissionFormDialog";
import { PermissionsTab } from "./PermissionsTab";

type Tab = "permissions" | "modules";
type Dialog =
  | { kind: "none" }
  | { kind: "module"; module?: ModuleItemDto }
  | { kind: "permission"; permission?: PermissionItem };

/** Permission management screen: modules and the permissions inside them. Everything comes from SQL via the API. */
export function PermissionsPage() {
  const [tab, setTab] = useState<Tab>("permissions");
  const [modules, setModules] = useState<ModuleItemDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Dialog>({ kind: "none" });
  const [refreshKey, setRefreshKey] = useState(0);

  const loadModules = useCallback(async () => {
    try {
      setModules(await getModules(true));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load modules.");
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadModules();
  }, [loadModules]);

  const saved = () => {
    setDialog({ kind: "none" });
    setRefreshKey((k) => k + 1);
    loadModules();
  };

  return (
    <div className="space-y-4">
      <h1 className="page-title">Permissions</h1>
      {error && <Alert kind="error">{error}</Alert>}

      <div role="tablist" className="tabs">
        <button role="tab" aria-selected={tab === "permissions"} className="tab" onClick={() => setTab("permissions")}>
          Permissions
        </button>
        <button role="tab" aria-selected={tab === "modules"} className="tab" onClick={() => setTab("modules")}>
          Modules
        </button>
      </div>

      {tab === "permissions" ? (
        <PermissionsTab
          modules={modules}
          refreshKey={refreshKey}
          onAdd={() => setDialog({ kind: "permission" })}
          onEdit={(permission) => setDialog({ kind: "permission", permission })}
        />
      ) : (
        <ModulesTab modules={modules} onAdd={() => setDialog({ kind: "module" })} onEdit={(module) => setDialog({ kind: "module", module })} />
      )}

      {dialog.kind === "module" && <ModuleFormDialog module={dialog.module} onClose={() => setDialog({ kind: "none" })} onSaved={saved} />}
      {dialog.kind === "permission" && (
        <PermissionFormDialog permission={dialog.permission} modules={modules} onClose={() => setDialog({ kind: "none" })} onSaved={saved} />
      )}
    </div>
  );
}
