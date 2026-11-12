"use client";

import { useState, type FormEvent } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { createGroup, updateGroup } from "@/services/groupService";
import { ApiError } from "@/types/api";
import type { GroupItem, GroupStatus } from "@/types/group";

/** Create (no `group`) or edit a group. The code is only editable on create. */
export function GroupFormDialog({ group, onClose, onSaved }: { group?: GroupItem; onClose: () => void; onSaved: () => void }) {
  const editing = !!group;
  const [code, setCode] = useState(group?.code ?? "");
  const [name, setName] = useState(group?.name ?? "");
  const [description, setDescription] = useState(group?.description ?? "");
  const [status, setStatus] = useState<GroupStatus>(group?.status ?? "Active");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (group) await updateGroup(group.id, { name, description: description || undefined, status });
      else await createGroup({ code, name, description: description || undefined });
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={editing ? "Edit group" : "Add group"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Code" required disabled={editing} value={code} onChange={(e) => setCode(e.target.value)} maxLength={50} placeholder="e.g. HR_Manager" />
        <TextField label="Name" required value={name} onChange={(e) => setName(e.target.value)} maxLength={100} placeholder="e.g. HR Manager" />
        <label className="block text-sm">
          Description
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={500}
            rows={3}
            className="mt-1 w-full rounded border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700"
          />
        </label>
        {editing && (
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={status === "Active"}
              disabled={group?.isSystem}
              onChange={(e) => setStatus(e.target.checked ? "Active" : "Inactive")}
            />
            Active
            {group?.isSystem ? (
              <span className="text-xs text-zinc-500">(system groups cannot be deactivated)</span>
            ) : (
              group && group.userCount > 0 && <span className="text-xs text-zinc-500">(an inactive group grants nothing to its {group.userCount} user(s))</span>
            )}
          </label>
        )}
        <SubmitButton busy={busy}>{editing ? "Save changes" : "Create group"}</SubmitButton>
      </form>
    </Modal>
  );
}
