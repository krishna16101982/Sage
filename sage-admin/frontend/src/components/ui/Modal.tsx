"use client";

import { useEffect, type ReactNode } from "react";

/** Dialog over the page. size "md" for forms, "xl" for wide content (permission matrix). Styles: .modal* in components.css. */
export function Modal({ title, onClose, children, size = "md" }: { title: string; onClose: () => void; children: ReactNode; size?: "md" | "xl" }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div role="dialog" aria-modal="true" aria-label={title} className={`modal modal-${size}`} onMouseDown={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="dialog-title">{title}</h2>
          <button type="button" aria-label="Close" onClick={onClose} className="modal-close">
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
