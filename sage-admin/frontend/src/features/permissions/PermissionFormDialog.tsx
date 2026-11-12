"use client";

import { useState, type FormEvent } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { createPermission, updatePermission } from "@/services/permissionService";
import { ApiError } from "@/types/api";
import type { ModuleItemDto, PermissionItem } from "@/types/permission";

/** Create (no `permission`) or edit a permission. Module and action are fixed after creation (they form the code). */
export function PermissionFormDialog({
  permission,
  modules,
  onClose,
  onSaved,
}: {
  permission?: PermissionItem;
  modules: ModuleItemDto[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = !!permission;
  const activeModules = modules.filter((m) => m.isActive);
  const [moduleId, setModuleId] = useState<number>(permission?.moduleId ?? activeModules[0]?.id ?? 0);
  const [action, setAction] = useState(permission?.action ?? "");
  const [name, setName] = useState(permission?.name ?? "");
  const [description, setDescription] = useState(permission?.description ?? "");
  const [isActive, setIsActive] = useState(permission?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const selectedModule = modules.find((m) => m.id === moduleId);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (permission) await updatePermission(permission.id, { name, description: description || undefined, isActive });
      else await createPermission({ moduleId, action, name, description: description || undefined });
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={editing ? `Edit ${permission.code}` : "Add permission"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        {!editing && (
          <>
            <label className="block text-sm">
              Module
              <select
                className="mt-1 w-full rounded border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700"
                value={moduleId}
                onChange={(e) => setModuleId(Number(e.target.value))}
              >
                {activeModules.map((m) => (
                  <option key={m.id} value={m.id} className="text-black">
                    {m.name} ({m.code})
                  </option>
                ))}
              </select>
            </label>
            <TextField label="Action" required value={action} onChange={(e) => setAction(e.target.value)} placeholder="e.g. View, Add, Approve" />
            <p className="text-xs text-zinc-500">
              Code will be <strong>{selectedModule?.code ?? "Module"}.{action || "Action"}</strong>
            </p>
          </>
        )}
        <TextField label="Name" required value={name} onChange={(e) => setName(e.target.value)} maxLength={150} />
        <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} />
        {editing && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isActive} disabled={permission?.isSystem} onChange={(e) => setIsActive(e.target.checked)} />
            Active {permission?.isSystem && <span className="text-xs text-zinc-500">(system permissions cannot be deactivated)</span>}
          </label>
        )}
        <SubmitButton busy={busy}>{editing ? "Save changes" : "Create permission"}</SubmitButton>
      </form>
    </Modal>
  );
}
