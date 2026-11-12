"use client";

export function Pagination({ page, totalPages, totalCount, onPage }: { page: number; totalPages: number; totalCount: number; onPage: (p: number) => void }) {
  return (
    <div className="pagination">
      <span>{totalCount} total</span>
      <div className="pagination-buttons">
        <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)} className="btn btn-secondary">
          Previous
        </button>
        <span>
          Page {page} of {Math.max(totalPages, 1)}
        </span>
        <button type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)} className="btn btn-secondary">
          Next
        </button>
      </div>
    </div>
  );
}
