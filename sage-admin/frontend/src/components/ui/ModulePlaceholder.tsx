/** Temporary body for module screens that are delivered in a later sprint. */
export function ModulePlaceholder({ title, sprint }: { title: string; sprint: number }) {
  return (
    <div className="space-y-2">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-sm text-zinc-500">This screen is delivered in Sprint {sprint}.</p>
    </div>
  );
}
