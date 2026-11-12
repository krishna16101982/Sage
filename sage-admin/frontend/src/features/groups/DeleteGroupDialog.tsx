"use client";

import { useState } from "react";
import { Alert } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { deleteGroup } from "@/services/groupService";
import { ApiError } from "@/types/api";
import type { GroupItem } from "@/types/group";

/** Confirms and deletes a group. The API refuses (409) while users are assigned; the message is shown as is. */
export function DeleteGroupDialog({ group, onClose, onDeleted }: { group: GroupItem; onClose: () => void; onDeleted: () => void }) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const hasUsers = group.userCount > 0;

  async function onDelete() {
    setError(null);
    setBusy(true);
    try {
      await deleteGroup(group.id);
      onDeleted();
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title="Delete group" onClose={onClose}>
      <div className="space-y-4 text-sm">
        {error && <Alert kind="error">{error}</Alert>}
        {hasUsers ? (
          <p>
            <strong>{group.name}</strong> has {group.userCount} user(s) assigned. Move them to another group before deleting it.
          </p>
        ) : (
          <p>
            Delete <strong>{group.name}</strong> and its {group.permissionCount} permission grant(s)? This cannot be undone.
          </p>
        )}
        <div className="modal-footer">
          <button type="button" onClick={onClose} className="btn btn-secondary">
            Cancel
          </button>
          <button
            type="button"
            onClick={onDelete}
            disabled={busy || hasUsers}
            className="btn btn-danger"
          >
            {busy ? "Deleting..." : "Delete"}
          </button>
        </div>
      </div>
    </Modal>
  );
}
