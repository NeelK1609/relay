import Link from "next/link";
import { knowledgeApi } from "@/lib/api";
import { CASE_CATEGORIES, CATEGORY_LABELS } from "@/lib/types";

export const metadata = { title: "Help centre" };

export default async function HelpCentre({ searchParams }: PageProps<"/help">) {
  const { q, category } = await searchParams;
  const term = typeof q === "string" ? q : undefined;
  const cat = typeof category === "string" ? category : undefined;
  const articles = await knowledgeApi.search(term, cat);

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <h1 className="text-2xl font-semibold text-slate-900">Help centre</h1>
      <form className="mt-5 flex flex-wrap gap-2">
        <input
          name="q"
          defaultValue={term}
          placeholder="Search articles"
          aria-label="Search articles"
          className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-emerald-500 focus:ring-2 focus:ring-emerald-200"
        />
        <select
          name="category"
          defaultValue={cat ?? ""}
          aria-label="Topic"
          className="rounded-lg border border-slate-300 bg-white px-3 py-2"
        >
          <option value="">All topics</option>
          {CASE_CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {CATEGORY_LABELS[c]}
            </option>
          ))}
        </select>
        <button className="rounded-lg bg-emerald-600 px-4 py-2 font-medium text-white hover:bg-emerald-700">Search</button>
      </form>

      {articles.length === 0 ? (
        <div className="mt-8 rounded-xl border border-dashed border-slate-300 p-6 text-center">
          <p className="text-slate-700">No articles match your search.</p>
          <Link href="/requests/new" className="mt-2 inline-block font-medium text-emerald-700 hover:underline">
            Ask our team instead →
          </Link>
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {articles.map((a) => (
            <li key={a.id}>
              <Link href={`/help/${a.id}`} className="block rounded-xl border border-slate-200 bg-white p-4 hover:border-emerald-400">
                <p className="text-xs font-medium uppercase tracking-wide text-emerald-700">{CATEGORY_LABELS[a.category]}</p>
                <p className="mt-1 font-medium text-slate-900">{a.title}</p>
                <p className="mt-1 line-clamp-2 text-sm text-slate-500">{a.body}</p>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
