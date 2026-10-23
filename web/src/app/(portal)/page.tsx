import Link from "next/link";
import { knowledgeApi } from "@/lib/api";
import { CATEGORY_LABELS, type CaseCategory } from "@/lib/types";

const TOPICS: { category: CaseCategory; icon: string; blurb: string }[] = [
  { category: "Outage", icon: "🔌", blurb: "Report an outage or flickering lights" },
  { category: "Billing", icon: "🧾", blurb: "Questions about your bill or usage" },
  { category: "Payment", icon: "💳", blurb: "Payments and payment arrangements" },
  { category: "MoveService", icon: "📦", blurb: "Start, stop or transfer service" },
  { category: "Meter", icon: "📟", blurb: "Meter readings and error codes" },
  { category: "General", icon: "💬", blurb: "Anything else" },
];

export default async function Home() {
  const articles = await knowledgeApi.search();

  return (
    <>
      <div className="bg-red-50 text-red-900">
        <p className="mx-auto max-w-5xl px-4 py-2 text-sm">
          <strong>Downed power line?</strong> Stay 10 metres away and call 911, then our emergency line at 1-888-555-0199.
        </p>
      </div>

      <section className="bg-gradient-to-b from-emerald-50 to-white">
        <div className="mx-auto max-w-5xl px-4 py-14 sm:py-20">
          <h1 className="text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl">How can we help?</h1>
          <p className="mt-3 max-w-xl text-slate-600">
            Search our help centre, or start a request and our team will get back to you.
          </p>
          <form action="/help" className="mt-6 flex max-w-xl gap-2">
            <label htmlFor="q" className="sr-only">
              Search help articles
            </label>
            <input
              id="q"
              name="q"
              placeholder="e.g. high bill, payment arrangement, Err 04"
              className="min-w-0 flex-1 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-slate-900 shadow-sm outline-none focus:border-emerald-500 focus:ring-2 focus:ring-emerald-200"
            />
            <button className="rounded-lg bg-emerald-600 px-4 py-2.5 font-medium text-white hover:bg-emerald-700">
              Search
            </button>
          </form>
        </div>
      </section>

      <section className="mx-auto max-w-5xl px-4 py-10">
        <h2 className="text-lg font-semibold text-slate-900">Start a request</h2>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {TOPICS.map((t) => (
            <Link
              key={t.category}
              href={`/requests/new?category=${t.category}`}
              className="group rounded-xl border border-slate-200 bg-white p-4 transition hover:border-emerald-400 hover:shadow-sm"
            >
              <span className="text-2xl" aria-hidden>
                {t.icon}
              </span>
              <p className="mt-2 font-medium text-slate-900 group-hover:text-emerald-700">{CATEGORY_LABELS[t.category]}</p>
              <p className="text-sm text-slate-500">{t.blurb}</p>
            </Link>
          ))}
        </div>
      </section>

      <section className="mx-auto max-w-5xl px-4 pb-14">
        <h2 className="text-lg font-semibold text-slate-900">Popular articles</h2>
        <ul className="mt-4 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {articles.slice(0, 6).map((a) => (
            <li key={a.id}>
              <Link href={`/help/${a.id}`} className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-slate-50">
                <span className="text-slate-800">{a.title}</span>
                <span className="text-xs text-slate-400">{CATEGORY_LABELS[a.category]}</span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </>
  );
}
