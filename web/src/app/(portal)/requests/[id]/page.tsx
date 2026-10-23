import Link from "next/link";
import { AutoRefresh } from "@/components/auto-refresh";
import { notFound, redirect } from "next/navigation";
import { StatusBadge } from "@/components/badges";
import { portalApi } from "@/lib/api";
import { dateTime } from "@/lib/format";
import { getCustomerId } from "@/lib/session";
import { CATEGORY_LABELS } from "@/lib/types";
import { replyToRequest } from "../../actions";

export default async function RequestDetail({ params, searchParams }: PageProps<"/requests/[id]">) {
  const customerId = await getCustomerId();
  if (!customerId) redirect("/sign-in");
  const { id } = await params;
  const { created, error } = await searchParams;
  const detail = await portalApi.getCase(customerId, id);
  if (!detail) notFound();
  const { summary, messages } = detail;
  const canReply = summary.status !== "Closed";

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <AutoRefresh />
      <Link href="/requests" className="text-sm text-slate-500 hover:text-slate-800">
        ← My requests
      </Link>

      {created && (
        <p role="status" className="mt-4 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          Thanks, we&apos;ve received your request. Your reference is <strong>{summary.reference}</strong>.
        </p>
      )}
      {typeof error === "string" && (
        <p role="alert" className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">
          {error}
        </p>
      )}

      <div className="mt-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">{summary.title}</h1>
          <p className="mt-1 text-sm text-slate-500">
            {summary.reference} · {CATEGORY_LABELS[summary.category]} · opened {dateTime(summary.createdAt)}
          </p>
        </div>
        <StatusBadge status={summary.status} />
      </div>

      <ol className="mt-8 space-y-4">
        {messages.map((m) => {
          const mine = m.authorType === "Customer";
          const ai = m.authorType === "AiAgent";
          return (
            <li key={m.id} className={`flex ${mine ? "justify-end" : "justify-start"}`}>
              <div
                className={`max-w-[85%] rounded-2xl px-4 py-3 ${
                  mine
                    ? "bg-emerald-600 text-white"
                    : ai
                      ? "border border-violet-200 bg-violet-50 text-slate-800"
                      : "border border-slate-200 bg-white text-slate-800"
                }`}
              >
                <p className={`text-xs ${mine ? "text-emerald-100" : "text-slate-500"}`}>
                  {mine ? "You" : ai ? "Relay Assistant (AI) · Highland Energy" : `${m.authorName} · Highland Energy`} ·{" "}
                  {dateTime(m.createdAt)}
                </p>
                <p className="mt-1 whitespace-pre-wrap leading-6">{m.body}</p>
              </div>
            </li>
          );
        })}
      </ol>

      {canReply ? (
        <form action={replyToRequest.bind(null, summary.id)} className="mt-8">
          <label htmlFor="body" className="text-sm font-medium text-slate-800">
            Add a reply
          </label>
          <textarea
            id="body"
            name="body"
            required
            rows={4}
            maxLength={4000}
            className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-emerald-500 focus:ring-2 focus:ring-emerald-200"
          />
          <button className="mt-2 rounded-lg bg-emerald-600 px-4 py-2 font-medium text-white hover:bg-emerald-700">Send</button>
        </form>
      ) : (
        <p className="mt-8 text-sm text-slate-500">
          This request is closed. <Link href="/requests/new" className="text-emerald-700 hover:underline">Start a new one</Link> if you
          need more help.
        </p>
      )}
    </div>
  );
}
