"use client";

import { useEffect, type ReactNode } from "react";

const WIDTH = { md: "max-w-md", xl: "max-w-5xl" } as const;

export function Modal({ title, onClose, children, size = "md" }: { title: string; onClose: () => void; children: ReactNode; size?: keyof typeof WIDTH }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onMouseDown={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`w-full ${WIDTH[size]} max-h-[90vh] space-y-4 overflow-y-auto rounded-lg border border-zinc-200 bg-white p-6 text-zinc-900 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-100`}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button type="button" aria-label="Close" onClick={onClose} className="text-xl leading-none">
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
