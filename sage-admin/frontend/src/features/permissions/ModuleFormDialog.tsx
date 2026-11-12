"use client";

import { useState, type FormEvent } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { createModule, updateModule } from "@/services/permissionService";
import { ApiError } from "@/types/api";
import type { ModuleItemDto } from "@/types/permission";

/** Create (no `module`) or edit a module. The code is only editable on create: permission codes derive from it. */
export function ModuleFormDialog({ module, onClose, onSaved }: { module?: ModuleItemDto; onClose: () => void; onSaved: () => void }) {
  const editing = !!module;
  const [code, setCode] = useState(module?.code ?? "");
  const [name, setName] = useState(module?.name ?? "");
  const [category, setCategory] = useState(module?.category ?? "");
  const [sortOrder, setSortOrder] = useState(String(module?.sortOrder ?? 0));
  const [isActive, setIsActive] = useState(module?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const order = Number.parseInt(sortOrder, 10) || 0;
      if (module) await updateModule(module.id, { name, category: category || undefined, sortOrder: order, isActive });
      else await createModule({ code, name, category: category || undefined, sortOrder: order });
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={editing ? "Edit module" : "Add module"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Code" required disabled={editing} value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. Inventory" />
        <TextField label="Name" required value={name} onChange={(e) => setName(e.target.value)} maxLength={100} />
        <TextField label="Category" value={category} onChange={(e) => setCategory(e.target.value)} maxLength={100} placeholder="e.g. HR, Sales" />
        <TextField label="Sort order" type="number" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        {editing && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isActive} disabled={module?.isSystem} onChange={(e) => setIsActive(e.target.checked)} />
            Active {module?.isSystem && <span className="text-xs text-zinc-500">(system modules cannot be deactivated)</span>}
          </label>
        )}
        <SubmitButton busy={busy}>{editing ? "Save changes" : "Create module"}</SubmitButton>
      </form>
    </Modal>
  );
}
