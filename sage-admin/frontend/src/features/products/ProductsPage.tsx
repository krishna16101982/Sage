"use client";

import { useCallback, useEffect, useState } from "react";
import { Can } from "@/components/guards/Can";
import { Alert } from "@/components/ui/Form";
import { getCatalog } from "@/services/productService";
import { ApiError } from "@/types/api";
import type { Product, ProductCategory, ProductPlan } from "@/types/product";
import { formatDuration, formatInr } from "@/utils/format";
import { Perm } from "@/utils/permissionCodes";
import { CategoryFormDialog, PlanFormDialog, ProductFormDialog } from "./CatalogDialogs";

type Dialog =
  | { kind: "none" }
  | { kind: "category"; category?: ProductCategory }
  | { kind: "product"; categoryId: number; product?: Product }
  | { kind: "plan"; product: Product; plan?: ProductPlan };

const button = "btn btn-primary"; // styles/components.css
const link = "link-button";

function Status({ active }: { active: boolean }) {
  return active ? null : <span className="badge">inactive</span>;
}

/** Product catalog: categories, their products and each product's plans (price list). Everything comes from SQL via the API. */
export function ProductsPage() {
  const [showInactive, setShowInactive] = useState(true);
  const [catalog, setCatalog] = useState<ProductCategory[] | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Dialog>({ kind: "none" });

  const load = useCallback(async () => {
    try {
      setCatalog(await getCatalog(showInactive));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load the product catalog.");
    }
  }, [showInactive]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  const saved = () => {
    setDialog({ kind: "none" });
    load();
  };

  const toggle = (productId: number) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(productId)) next.delete(productId);
      else next.add(productId);
      return next;
    });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="page-title">Products</h1>
        <div className="toolbar-spacer" />
        <label className="checkbox-label">
          <input type="checkbox" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
          Show inactive
        </label>
        <Can permission={Perm.ProductAdd}>
          <button type="button" className={button} onClick={() => setDialog({ kind: "category" })}>
            Add category
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {!catalog && !error && <p className="text-sm text-muted">Loading...</p>}
      {catalog?.length === 0 && <p className="text-sm text-muted">No categories yet.</p>}

      {catalog?.map((c) => (
        <section key={c.id} className="card">
          <header className="card-header">
            <div>
              <h2 className="section-title">
                {c.name}
                <Status active={c.isActive} />
              </h2>
              {c.description && <p className="hint">{c.description}</p>}
            </div>
            <div className="toolbar-spacer" />
            <Can permission={Perm.ProductEdit}>
              <button type="button" className={`${link} text-sm`} onClick={() => setDialog({ kind: "category", category: c })}>
                Edit category
              </button>
            </Can>
            {c.isActive && (
              <Can permission={Perm.ProductAdd}>
                <button type="button" className={`${link} text-sm`} onClick={() => setDialog({ kind: "product", categoryId: c.id })}>
                  Add product
                </button>
              </Can>
            )}
          </header>

          {c.products.length === 0 ? (
            <p className="card-body text-sm text-muted">No products in this category.</p>
          ) : (
            <ul className="card-list">
              {c.products.map((p) => {
                const open = expanded.has(p.id);
                return (
                  <li key={p.id}>
                    <div className="flex flex-wrap items-center gap-3">
                      <button type="button" aria-expanded={open} onClick={() => toggle(p.id)} className="flex items-center gap-2 text-left">
                        <span className="w-3 text-muted">{open ? "▾" : "▸"}</span>
                        <span className="font-medium">{p.name}</span>
                        <span className="code hint">{p.code}</span>
                        <Status active={p.isActive} />
                      </button>
                      <span className="hint">
                        {p.plans.length} plan{p.plans.length === 1 ? "" : "s"}
                      </span>
                      <div className="toolbar-spacer" />
                      <Can permission={Perm.ProductEdit}>
                        <button type="button" className={link} onClick={() => setDialog({ kind: "product", categoryId: c.id, product: p })}>
                          Edit
                        </button>
                      </Can>
                      {p.isActive && (
                        <Can permission={Perm.ProductAdd}>
                          <button type="button" className={link} onClick={() => setDialog({ kind: "plan", product: p })}>
                            Add plan
                          </button>
                        </Can>
                      )}
                    </div>

                    {open && (
                      <div className="table-wrap mt-2 ml-5">
                        <table className="data-table">
                          <thead>
                            <tr>
                              <th>Plan</th>
                              <th>Duration</th>
                              <th className="amount">Price</th>
                              <th className="amount">Cost</th>
                              <th className="amount">Margin</th>
                              <th />
                            </tr>
                          </thead>
                          <tbody>
                            {p.plans.map((plan) => (
                              <tr key={plan.id}>
                                <td>
                                  {plan.name}
                                  <Status active={plan.isActive} />
                                  {plan.description && <div className="hint">{plan.description}</div>}
                                </td>
                                <td>{formatDuration(plan.durationCount, plan.durationUnit)}</td>
                                <td className="amount">{formatInr(plan.price)}</td>
                                <td className="amount">{formatInr(plan.cost)}</td>
                                <td className={`amount ${plan.price - plan.cost < 0 ? "text-danger" : ""}`}>
                                  {formatInr(plan.price - plan.cost)}
                                </td>
                                <td className="actions">
                                  <Can permission={Perm.ProductEdit}>
                                    <button type="button" className={link} onClick={() => setDialog({ kind: "plan", product: p, plan })}>
                                      Edit
                                    </button>
                                  </Can>
                                </td>
                              </tr>
                            ))}
                            {p.plans.length === 0 && (
                              <tr>
                                <td colSpan={6} className="table-empty">
                                  No plans yet. A product needs at least one active plan before it can be sold.
                                </td>
                              </tr>
                            )}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      ))}

      {dialog.kind === "category" && <CategoryFormDialog category={dialog.category} onClose={() => setDialog({ kind: "none" })} onSaved={saved} />}
      {dialog.kind === "product" && catalog && (
        <ProductFormDialog
          product={dialog.product}
          categoryId={dialog.categoryId}
          categories={catalog}
          onClose={() => setDialog({ kind: "none" })}
          onSaved={saved}
        />
      )}
      {dialog.kind === "plan" && <PlanFormDialog product={dialog.product} plan={dialog.plan} onClose={() => setDialog({ kind: "none" })} onSaved={saved} />}
    </div>
  );
}
