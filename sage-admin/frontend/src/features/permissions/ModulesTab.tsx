"use client";

import { Can } from "@/components/guards/Can";
import { Perm } from "@/utils/permissionCodes";
import type { ModuleItemDto } from "@/types/permission";

export function ModulesTab({ modules, onAdd, onEdit }: { modules: ModuleItemDto[]; onAdd: () => void; onEdit: (m: ModuleItemDto) => void }) {
  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Can permission={Perm.PermissionAdd}>
          <button type="button" onClick={onAdd} className="btn btn-primary">
            Add module
          </button>
        </Can>
      </div>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Category</th>
              <th>Order</th>
              <th>Permissions</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {modules.map((m) => (
              <tr key={m.id}>
                <td className="code">{m.code}</td>
                <td>{m.name}</td>
                <td>{m.category ?? "-"}</td>
                <td>{m.sortOrder}</td>
                <td>{m.permissionCount}</td>
                <td>
                  {m.isActive ? "Active" : "Inactive"}
                  {m.isSystem && <span className="badge">system</span>}
                </td>
                <td className="actions">
                  <Can permission={Perm.PermissionEdit}>
                    <button type="button" className="link-button" onClick={() => onEdit(m)}>
                      Edit
                    </button>
                  </Can>
                </td>
              </tr>
            ))}
            {modules.length === 0 && (
              <tr>
                <td colSpan={7} className="table-empty">
                  No modules.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
