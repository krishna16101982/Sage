"use client";

import { Can } from "@/components/guards/Can";
import { Perm } from "@/utils/permissionCodes";
import type { ModuleItemDto } from "@/types/permission";

export function ModulesTab({ modules, onAdd, onEdit }: { modules: ModuleItemDto[]; onAdd: () => void; onEdit: (m: ModuleItemDto) => void }) {
  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Can permission={Perm.PermissionAdd}>
          <button type="button" onClick={onAdd} className="rounded bg-zinc-900 px-3 py-2 text-sm text-white dark:bg-zinc-100 dark:text-zinc-900">
            Add module
          </button>
        </Can>
      </div>
      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 dark:bg-zinc-900">
            <tr>
              <th className="px-3 py-2">Code</th>
              <th className="px-3 py-2">Name</th>
              <th className="px-3 py-2">Category</th>
              <th className="px-3 py-2">Order</th>
              <th className="px-3 py-2">Permissions</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {modules.map((m) => (
              <tr key={m.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2 font-mono">{m.code}</td>
                <td className="px-3 py-2">{m.name}</td>
                <td className="px-3 py-2">{m.category ?? "-"}</td>
                <td className="px-3 py-2">{m.sortOrder}</td>
                <td className="px-3 py-2">{m.permissionCount}</td>
                <td className="px-3 py-2">
                  {m.isActive ? "Active" : "Inactive"}
                  {m.isSystem && <span className="ml-2 text-xs text-zinc-500">system</span>}
                </td>
                <td className="px-3 py-2 text-right">
                  <Can permission={Perm.PermissionEdit}>
                    <button type="button" className="underline" onClick={() => onEdit(m)}>
                      Edit
                    </button>
                  </Can>
                </td>
              </tr>
            ))}
            {modules.length === 0 && (
              <tr>
                <td colSpan={7} className="px-3 py-6 text-center text-zinc-500">
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
