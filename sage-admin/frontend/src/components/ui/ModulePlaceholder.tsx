/** Temporary body for module screens that are delivered in a later sprint. */
export function ModulePlaceholder({ title }: { title: string }) {
  return (
    <div className="space-y-2">
      <h1 className="page-title">{title}</h1>
      <p className="text-muted text-sm">This screen is coming in a later sprint.</p>
    </div>
  );
}
