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

const button = "rounded bg-zinc-900 px-3 py-2 text-sm text-white dark:bg-zinc-100 dark:text-zinc-900";
const link = "underline";

function Status({ active }: { active: boolean }) {
  return active ? null : <span className="ml-2 rounded bg-zinc-200 px-1.5 py-0.5 text-xs text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">inactive</span>;
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
        <h1 className="text-2xl font-semibold">Products</h1>
        <div className="flex-1" />
        <label className="flex items-center gap-2 text-sm">
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
      {!catalog && !error && <p className="text-sm text-zinc-500">Loading...</p>}
      {catalog?.length === 0 && <p className="text-sm text-zinc-500">No categories yet.</p>}

      {catalog?.map((c) => (
        <section key={c.id} className="rounded border border-zinc-200 dark:border-zinc-800">
          <header className="flex flex-wrap items-center gap-3 border-b border-zinc-200 bg-zinc-50 px-4 py-3 dark:border-zinc-800 dark:bg-zinc-900">
            <div>
              <h2 className="font-semibold">
                {c.name}
                <Status active={c.isActive} />
              </h2>
              {c.description && <p className="text-xs text-zinc-500">{c.description}</p>}
            </div>
            <div className="flex-1" />
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
            <p className="px-4 py-4 text-sm text-zinc-500">No products in this category.</p>
          ) : (
            <ul className="divide-y divide-zinc-200 dark:divide-zinc-800">
              {c.products.map((p) => {
                const open = expanded.has(p.id);
                return (
                  <li key={p.id} className="px-4 py-2 text-sm">
                    <div className="flex flex-wrap items-center gap-3">
                      <button type="button" aria-expanded={open} onClick={() => toggle(p.id)} className="flex items-center gap-2 text-left">
                        <span className="w-3 text-zinc-500">{open ? "▾" : "▸"}</span>
                        <span className="font-medium">{p.name}</span>
                        <span className="font-mono text-xs text-zinc-500">{p.code}</span>
                        <Status active={p.isActive} />
                      </button>
                      <span className="text-xs text-zinc-500">
                        {p.plans.length} plan{p.plans.length === 1 ? "" : "s"}
                      </span>
                      <div className="flex-1" />
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
                      <div className="mt-2 ml-5 overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
                        <table className="w-full text-left text-sm">
                          <thead className="bg-zinc-50 dark:bg-zinc-900">
                            <tr>
                              <th className="px-3 py-2">Plan</th>
                              <th className="px-3 py-2">Duration</th>
                              <th className="px-3 py-2 text-right">Price</th>
                              <th className="px-3 py-2 text-right">Cost</th>
                              <th className="px-3 py-2 text-right">Margin</th>
                              <th className="px-3 py-2" />
                            </tr>
                          </thead>
                          <tbody>
                            {p.plans.map((plan) => (
                              <tr key={plan.id} className="border-t border-zinc-200 dark:border-zinc-800">
                                <td className="px-3 py-2">
                                  {plan.name}
                                  <Status active={plan.isActive} />
                                  {plan.description && <div className="text-xs text-zinc-500">{plan.description}</div>}
                                </td>
                                <td className="px-3 py-2">{formatDuration(plan.durationCount, plan.durationUnit)}</td>
                                <td className="px-3 py-2 text-right tabular-nums">{formatInr(plan.price)}</td>
                                <td className="px-3 py-2 text-right tabular-nums">{formatInr(plan.cost)}</td>
                                <td className={`px-3 py-2 text-right tabular-nums ${plan.price - plan.cost < 0 ? "text-red-600" : ""}`}>
                                  {formatInr(plan.price - plan.cost)}
                                </td>
                                <td className="px-3 py-2 text-right">
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
                                <td colSpan={6} className="px-3 py-4 text-center text-zinc-500">
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
