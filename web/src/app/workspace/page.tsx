import Link from "next/link";
import { AutoRefresh } from "@/components/auto-refresh";
import { SentimentBadge } from "@/components/ai-panel";
import { AskRelay } from "@/components/ask-relay";
import { askRelay } from "./actions";
import { CategoryBadge, PriorityBadge, StatusBadge } from "@/components/badges";
import { SlaIndicator } from "@/components/sla";
import { workspaceApi, type CaseFilters } from "@/lib/api";
import { relativeTime } from "@/lib/format";
import { CASE_CATEGORIES, CASE_PRIORITIES, CASE_STATUSES, CATEGORY_LABELS, STATUS_LABELS } from "@/lib/types";

export const metadata = { title: "Queue" };

export default async function Queue({ searchParams }: PageProps<"/workspace">) {
  const sp = await searchParams;
  const str = (k: string) => (typeof sp[k] === "string" ? (sp[k] as string) : undefined);
  const filters: CaseFilters = {
    status: str("status"),
    priority: str("priority"),
    category: str("category"),
    assignedStaffId: str("assignedStaffId"),
    search: str("search"),
    openOnly: str("openOnly") ?? (str("status") ? undefined : "true"),
  };
  const [stats, cases] = await Promise.all([workspaceApi.stats(), workspaceApi.cases(filters)]);

  const tiles = [
    { label: "Open cases", value: stats.open },
    { label: "New, untouched", value: stats.new },
    { label: "Waiting on customer", value: stats.waitingOnCustomer },
    { label: "Response overdue", value: stats.firstResponseBreached, alert: stats.firstResponseBreached > 0 },
    { label: "Resolved today", value: stats.resolvedToday },
  ];
  const select = "rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm";

  return (
    <div className="px-4 py-6 lg:px-8">
      <AutoRefresh seconds={20} />
      <h1 className="text-xl font-semibold text-slate-900">Case queue</h1>

      <dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-5">
        {tiles.map((t) => (
          <div key={t.label} className="rounded-xl border border-slate-200 bg-white p-4">
            <dt className="text-xs text-slate-500">{t.label}</dt>
            <dd className={`mt-1 text-2xl font-semibold ${t.alert ? "text-red-700" : "text-slate-900"}`}>{t.value}</dd>
          </div>
        ))}
      </dl>

      <AskRelay ask={askRelay} />

      <form className="mt-6 flex flex-wrap items-center gap-2">
        <input
          name="search"
          defaultValue={filters.search}
          placeholder="Search title, customer, CAS-…, account"
          aria-label="Search cases"
          className="w-full rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm sm:w-auto sm:min-w-0 sm:max-w-xs sm:flex-1"
        />
        <select name="status" defaultValue={filters.status ?? ""} aria-label="Status" className={select}>
          <option value="">All open</option>
          {CASE_STATUSES.map((s) => (
            <option key={s} value={s}>
              {STATUS_LABELS[s]}
            </option>
          ))}
        </select>
        <select name="priority" defaultValue={filters.priority ?? ""} aria-label="Priority" className={select}>
          <option value="">Any priority</option>
          {CASE_PRIORITIES.map((p) => (
            <option key={p}>{p}</option>
          ))}
        </select>
        <select name="category" defaultValue={filters.category ?? ""} aria-label="Category" className={select}>
          <option value="">Any category</option>
          {CASE_CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {CATEGORY_LABELS[c]}
            </option>
          ))}
        </select>
        {filters.assignedStaffId && <input type="hidden" name="assignedStaffId" value={filters.assignedStaffId} />}
        <button className="rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-700">Filter</button>
        <Link href="/workspace" className="text-sm text-slate-500 hover:text-slate-800">
          Reset
        </Link>
      </form>

      <div className="mt-4 overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="w-full min-w-[820px] text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-2 font-medium">Case</th>
              <th className="px-4 py-2 font-medium">Customer</th>
              <th className="px-4 py-2 font-medium">Priority</th>
              <th className="px-4 py-2 font-medium">Status</th>
              <th className="px-4 py-2 font-medium">Owner</th>
              <th className="px-4 py-2 font-medium">SLA</th>
              <th className="px-4 py-2 font-medium">Updated</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {cases.map((c) => (
              <tr key={c.id} className="hover:bg-slate-50">
                <td className="px-4 py-3">
                  <Link href={`/workspace/cases/${c.id}`} className="font-medium text-slate-900 hover:text-indigo-700">
                    {c.title}
                  </Link>
                  {c.safetyRisk && (
                    <span className="ml-2 rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-800">⚠ Safety</span>
                  )}
                  {(c.sentiment === "Negative" || c.sentiment === "Distressed") && (
                    <span className="ml-2">
                      <SentimentBadge sentiment={c.sentiment} />
                    </span>
                  )}
                  <div className="mt-1 flex items-center gap-2 text-xs text-slate-500">
                    {c.reference} <CategoryBadge category={c.category} /> {c.channel}
                  </div>
                </td>
                <td className="px-4 py-3 text-slate-700">
                  {c.customerName}
                  <div className="text-xs text-slate-400">{c.accountNumber}</div>
                </td>
                <td className="px-4 py-3">
                  <PriorityBadge priority={c.priority} />
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={c.status} />
                </td>
                <td className="px-4 py-3 text-slate-700">{c.assignedStaffName ?? <span className="text-slate-400">Unassigned</span>}</td>
                <td className="px-4 py-3">
                  <SlaIndicator c={c} />
                </td>
                <td className="px-4 py-3 text-slate-500">{relativeTime(c.updatedAt)}</td>
              </tr>
            ))}
            {cases.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-slate-500">
                  No cases match these filters.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
