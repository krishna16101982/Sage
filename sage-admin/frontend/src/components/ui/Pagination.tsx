"use client";

export function Pagination({ page, totalPages, totalCount, onPage }: { page: number; totalPages: number; totalCount: number; onPage: (p: number) => void }) {
  return (
    <div className="flex items-center justify-between text-sm">
      <span>{totalCount} total</span>
      <div className="flex items-center gap-2">
        <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)} className="rounded border px-3 py-1 disabled:opacity-40">
          Previous
        </button>
        <span>
          Page {page} of {Math.max(totalPages, 1)}
        </span>
        <button type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)} className="rounded border px-3 py-1 disabled:opacity-40">
          Next
        </button>
      </div>
    </div>
  );
}
