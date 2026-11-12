"use client";

import { useState, type FormEvent, type ReactNode } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { createCategory, createPlan, createProduct, updateCategory, updatePlan, updateProduct } from "@/services/productService";
import { ApiError } from "@/types/api";
import { DURATION_UNITS, type DurationUnit, type Product, type ProductCategory, type ProductPlan } from "@/types/product";
import { formatInr } from "@/utils/format";

const field = "mt-1 w-full rounded border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700";
const errorText = (err: unknown) => (err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");

/** Shared shell: submit handling, error display, busy state. */
function useSubmit(onSaved: () => void) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = (work: () => Promise<unknown>) => async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await work();
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };
  return { error, busy, submit };
}

function Description({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <label className="block text-sm">
      Description
      <textarea value={value} onChange={(e) => onChange(e.target.value)} maxLength={500} rows={2} className={field} />
    </label>
  );
}

function ActiveToggle({ checked, onChange, note }: { checked: boolean; onChange: (v: boolean) => void; note?: ReactNode }) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      Active {note && <span className="text-xs text-zinc-500">{note}</span>}
    </label>
  );
}

const toInt = (v: string) => Number.parseInt(v, 10) || 0;

/** Create (no `category`) or edit a category. Code only on create. */
export function CategoryFormDialog({ category, onClose, onSaved }: { category?: ProductCategory; onClose: () => void; onSaved: () => void }) {
  const [code, setCode] = useState(category?.code ?? "");
  const [name, setName] = useState(category?.name ?? "");
  const [description, setDescription] = useState(category?.description ?? "");
  const [sortOrder, setSortOrder] = useState(String(category?.sortOrder ?? 0));
  const [isActive, setIsActive] = useState(category?.isActive ?? true);
  const { error, busy, submit } = useSubmit(onSaved);

  const onSubmit = submit(() =>
    category
      ? updateCategory(category.id, { name, description: description || undefined, sortOrder: toInt(sortOrder), isActive })
      : createCategory({ code, name, description: description || undefined, sortOrder: toInt(sortOrder) }),
  );

  return (
    <Modal title={category ? "Edit category" : "Add category"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Code" required disabled={!!category} value={code} onChange={(e) => setCode(e.target.value)} maxLength={50} placeholder="e.g. CyberSecurity" />
        <TextField label="Name" required value={name} onChange={(e) => setName(e.target.value)} maxLength={100} />
        <Description value={description} onChange={setDescription} />
        <TextField label="Sort order" type="number" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        {category && <ActiveToggle checked={isActive} onChange={setIsActive} note="(inactive categories are hidden from the sale page)" />}
        <SubmitButton busy={busy}>{category ? "Save changes" : "Create category"}</SubmitButton>
      </form>
    </Modal>
  );
}

/** Create a product in `categoryId`, or edit `product` (which may move to another active category). */
export function ProductFormDialog({
  product,
  categoryId,
  categories,
  onClose,
  onSaved,
}: {
  product?: Product;
  categoryId: number;
  categories: ProductCategory[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [category, setCategory] = useState(product?.categoryId ?? categoryId);
  const [code, setCode] = useState(product?.code ?? "");
  const [name, setName] = useState(product?.name ?? "");
  const [description, setDescription] = useState(product?.description ?? "");
  const [sortOrder, setSortOrder] = useState(String(product?.sortOrder ?? 0));
  const [isActive, setIsActive] = useState(product?.isActive ?? true);
  const { error, busy, submit } = useSubmit(onSaved);

  const onSubmit = submit(() =>
    product
      ? updateProduct(product.id, { categoryId: category, name, description: description || undefined, sortOrder: toInt(sortOrder), isActive })
      : createProduct({ categoryId: category, code, name, description: description || undefined, sortOrder: toInt(sortOrder) }),
  );

  return (
    <Modal title={product ? "Edit product" : "Add product"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <label className="block text-sm">
          Category
          <select value={category} onChange={(e) => setCategory(Number(e.target.value))} className={field}>
            {categories
              .filter((c) => c.isActive || c.id === category)
              .map((c) => (
                <option key={c.id} value={c.id} className="text-black">
                  {c.name}
                </option>
              ))}
          </select>
        </label>
        <TextField label="Code" required disabled={!!product} value={code} onChange={(e) => setCode(e.target.value)} maxLength={50} placeholder="e.g. GreytHR" />
        <TextField label="Name" required value={name} onChange={(e) => setName(e.target.value)} maxLength={100} />
        <Description value={description} onChange={setDescription} />
        <TextField label="Sort order" type="number" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        {product && <ActiveToggle checked={isActive} onChange={setIsActive} note="(inactive products cannot be sold)" />}
        <SubmitButton busy={busy}>{product ? "Save changes" : "Create product"}</SubmitButton>
      </form>
    </Modal>
  );
}

/** Create a plan for `product`, or edit `plan`. Price and cost are per unit (INR) for one plan term. */
export function PlanFormDialog({ product, plan, onClose, onSaved }: { product: Product; plan?: ProductPlan; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(plan?.name ?? "");
  const [description, setDescription] = useState(plan?.description ?? "");
  const [durationUnit, setDurationUnit] = useState<DurationUnit>(plan?.durationUnit ?? "Year");
  const [durationCount, setDurationCount] = useState(String(plan?.durationCount ?? 1));
  const [price, setPrice] = useState(plan ? String(plan.price) : "");
  const [cost, setCost] = useState(plan ? String(plan.cost) : "");
  const [sortOrder, setSortOrder] = useState(String(plan?.sortOrder ?? 0));
  const [isActive, setIsActive] = useState(plan?.isActive ?? true);
  const { error, busy, submit } = useSubmit(onSaved);

  const priceNum = Number(price);
  const costNum = Number(cost);
  const showMargin = price !== "" && cost !== "" && Number.isFinite(priceNum) && Number.isFinite(costNum);

  const onSubmit = submit(() => {
    const data = {
      name,
      description: description || undefined,
      durationUnit,
      durationCount: toInt(durationCount),
      price: priceNum,
      cost: costNum,
      sortOrder: toInt(sortOrder),
    };
    return plan ? updatePlan(plan.id, { ...data, isActive }) : createPlan(product.id, data);
  });

  return (
    <Modal title={plan ? `Edit plan: ${product.name}` : `Add plan: ${product.name}`} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Plan name" required value={name} onChange={(e) => setName(e.target.value)} maxLength={100} placeholder="e.g. Business Yearly" />
        <Description value={description} onChange={setDescription} />
        <div className="grid grid-cols-2 gap-3">
          <TextField label="Duration" type="number" min={1} max={120} required value={durationCount} onChange={(e) => setDurationCount(e.target.value)} />
          <label className="block text-sm">
            Unit
            <select value={durationUnit} onChange={(e) => setDurationUnit(e.target.value as DurationUnit)} className={field}>
              {DURATION_UNITS.map((u) => (
                <option key={u} value={u} className="text-black">
                  {u}
                </option>
              ))}
            </select>
          </label>
          <TextField label="Price (₹ per unit)" type="number" min={0} step="0.01" required value={price} onChange={(e) => setPrice(e.target.value)} />
          <TextField label="Cost (₹ per unit)" type="number" min={0} step="0.01" required value={cost} onChange={(e) => setCost(e.target.value)} />
        </div>
        {showMargin && (
          <p className={`text-sm ${priceNum - costNum < 0 ? "text-red-600" : "text-zinc-500"}`}>
            Margin per unit: {formatInr(priceNum - costNum)}
            {priceNum > 0 && ` (${(((priceNum - costNum) / priceNum) * 100).toFixed(1)}%)`}
          </p>
        )}
        {plan && <p className="text-xs text-zinc-500">Changing price or cost affects new sales only. Existing sales keep the price they were sold at.</p>}
        <TextField label="Sort order" type="number" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
        {plan && <ActiveToggle checked={isActive} onChange={setIsActive} note="(inactive plans cannot be sold)" />}
        <SubmitButton busy={busy}>{plan ? "Save changes" : "Create plan"}</SubmitButton>
      </form>
    </Modal>
  );
}
