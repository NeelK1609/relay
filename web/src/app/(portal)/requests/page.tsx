import Link from "next/link";
import { AutoRefresh } from "@/components/auto-refresh";
import { redirect } from "next/navigation";
import { StatusBadge } from "@/components/badges";
import { portalApi } from "@/lib/api";
import { relativeTime } from "@/lib/format";
import { getCustomerId } from "@/lib/session";
import { CATEGORY_LABELS } from "@/lib/types";

export const metadata = { title: "My requests" };

export default async function MyRequests() {
  const customerId = await getCustomerId();
  if (!customerId) redirect("/sign-in");
  const cases = await portalApi.cases(customerId);

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <AutoRefresh seconds={20} />
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-slate-900">My requests</h1>
        <Link href="/requests/new" className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700">
          New request
        </Link>
      </div>

      {cases.length === 0 ? (
        <p className="mt-8 rounded-xl border border-dashed border-slate-300 p-6 text-center text-slate-600">
          You don&apos;t have any requests yet.
        </p>
      ) : (
        <ul className="mt-6 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {cases.map((c) => (
            <li key={c.id}>
              <Link href={`/requests/${c.id}`} className="flex flex-wrap items-center justify-between gap-3 px-4 py-4 hover:bg-slate-50">
                <span className="min-w-0">
                  <span className="block truncate font-medium text-slate-900">{c.title}</span>
                  <span className="block text-sm text-slate-500">
                    {c.reference} · {CATEGORY_LABELS[c.category]} · updated {relativeTime(c.updatedAt)}
                  </span>
                </span>
                <StatusBadge status={c.status} />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
