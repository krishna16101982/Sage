import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from "react";

/** Label + input. Styles: .field-label and .field in styles/components.css. */
export function TextField({ label, ...props }: { label: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="field-label">
      {label}
      <input {...props} className="field" />
    </label>
  );
}

/** Full-width main button used at the bottom of forms. */
export function SubmitButton({ busy, children, ...props }: { busy?: boolean; children: ReactNode } & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button type="submit" disabled={busy || props.disabled} {...props} className="btn btn-primary btn-block">
      {busy ? "Please wait..." : children}
    </button>
  );
}

export function Alert({ kind, children }: { kind: "error" | "success"; children: ReactNode }) {
  return (
    <div role={kind === "error" ? "alert" : "status"} className={`alert alert-${kind}`}>
      {children}
    </div>
  );
}

/** Centred card used by the sign-in, forgot-password and reset-password pages. */
export function AuthCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <main className="auth-page">
      <div className="auth-card">
        <h1 className="auth-title">{title}</h1>
        {children}
      </div>
    </main>
  );
}
