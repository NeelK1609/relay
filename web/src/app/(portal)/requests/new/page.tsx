import { redirect } from "next/navigation";
import { getCustomerId } from "@/lib/session";
import { CASE_CATEGORIES, CATEGORY_LABELS } from "@/lib/types";
import { openRequest } from "../../actions";

export const metadata = { title: "New request" };

export default async function NewRequest({ searchParams }: PageProps<"/requests/new">) {
  if (!(await getCustomerId())) redirect("/sign-in");
  const { category, error } = await searchParams;
  const selected = typeof category === "string" && (CASE_CATEGORIES as readonly string[]).includes(category) ? category : "General";

  const field = "mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-emerald-500 focus:ring-2 focus:ring-emerald-200";

  return (
    <div className="mx-auto max-w-2xl px-4 py-10">
      <h1 className="text-2xl font-semibold text-slate-900">Start a request</h1>
      <p className="mt-2 text-sm text-slate-600">
        Tell us what&apos;s happening. For a downed line or any danger, call 911 first.
      </p>

      {typeof error === "string" && (
        <p role="alert" className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">
          {error}
        </p>
      )}

      <form action={openRequest} className="mt-6 space-y-5">
        <div>
          <label htmlFor="category" className="text-sm font-medium text-slate-800">
            Topic
          </label>
          <select id="category" name="category" defaultValue={selected} className={field}>
            {CASE_CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {CATEGORY_LABELS[c]}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="title" className="text-sm font-medium text-slate-800">
            Subject
          </label>
          <input id="title" name="title" required maxLength={200} className={field} placeholder="A short summary" />
        </div>
        <div>
          <label htmlFor="description" className="text-sm font-medium text-slate-800">
            Details
          </label>
          <textarea
            id="description"
            name="description"
            required
            rows={6}
            maxLength={4000}
            className={field}
            placeholder="When did it start? What have you already tried?"
          />
        </div>
        <button className="rounded-lg bg-emerald-600 px-5 py-2.5 font-medium text-white hover:bg-emerald-700">
          Submit request
        </button>
      </form>
    </div>
  );
}
