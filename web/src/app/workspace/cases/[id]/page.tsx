import Link from "next/link";
import { notFound } from "next/navigation";
import { PriorityBadge, StatusBadge } from "@/components/badges";
import { SlaIndicator } from "@/components/sla";
import { workspaceApi } from "@/lib/api";
import { dateTime, money, relativeTime } from "@/lib/format";
import { getStaffId } from "@/lib/session";
import { CASE_CATEGORIES, CASE_PRIORITIES, CASE_STATUSES, CATEGORY_LABELS, STATUS_LABELS } from "@/lib/types";
import { AiPanel } from "@/components/ai-panel";
import { ReplyComposer } from "@/components/reply-composer";
import { LiveChatPanel } from "@/components/live-chat-panel";
import { draftReply, endStaffChat, joinChat, postMessage, refreshSummary, sendStaffChat, updateCase } from "../../actions";

const authorStyles = {
  Customer: "border-slate-200 bg-white",
  Staff: "border-indigo-200 bg-indigo-50",
  AiAgent: "border-violet-200 bg-violet-50",
  System: "border-slate-200 bg-slate-50",
};

export default async function CaseWorkspace({ params, searchParams }: PageProps<"/workspace/cases/[id]">) {
  const { id } = await params;
  const { error } = await searchParams;
  const [detail, staff, staffId, aiEnabled, chat] = await Promise.all([
    workspaceApi.getCase(id),
    workspaceApi.staff(),
    getStaffId(),
    workspaceApi.aiEnabled(id),
    workspaceApi.caseChat(id).catch(() => null),
  ]);
  if (!detail) notFound();
  const { summary: c, customer, messages } = detail;
  const otherCases = (await workspaceApi.cases({ search: customer.accountNumber })).filter((x) => x.id !== c.id);
  const field = "mt-1 w-full rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm";

  return (
    <div className="px-4 py-6 lg:px-8">
      <Link href="/workspace" className="text-sm text-slate-500 hover:text-slate-800">
        ← Queue
      </Link>
      <div className="mt-3 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">{c.title}</h1>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-slate-500">
            {c.reference} · via {c.channel} · opened {dateTime(c.createdAt)} <PriorityBadge priority={c.priority} />
            <StatusBadge status={c.status} />
          </p>
        </div>
        <div className="text-sm">
          <SlaIndicator c={c} />
        </div>
      </div>

      {typeof error === "string" && (
        <p role="alert" className="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-800">
          {error}
        </p>
      )}

      <div className="mt-6 grid gap-6 xl:grid-cols-[1fr_320px]">
        <section aria-label="Conversation" className="min-w-0">
          <ol className="space-y-3">
            {messages.map((m) => (
              <li
                key={m.id}
                className={`rounded-xl border p-4 ${m.isInternal ? "border-amber-200 bg-amber-50" : authorStyles[m.authorType]}`}
              >
                <p className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                  <span className="font-medium text-slate-800">{m.authorName}</span>
                  {m.authorType === "AiAgent" && <span className="rounded bg-violet-200 px-1.5 text-violet-900">AI</span>}
                  {m.isInternal && <span className="rounded bg-amber-200 px-1.5 text-amber-900">Internal note</span>}
                  <span>{dateTime(m.createdAt)}</span>
                </p>
                <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-800">{m.body}</p>
              </li>
            ))}
          </ol>

          {chat && (
            <LiveChatPanel
              state={chat}
              join={joinChat.bind(null, c.id)}
              send={sendStaffChat.bind(null, c.id)}
              end={endStaffChat.bind(null, c.id)}
            />
          )}

          {c.status !== "Closed" &&
            (staffId ? (
              <ReplyComposer action={postMessage.bind(null, c.id)} draft={draftReply.bind(null, c.id)} aiEnabled={aiEnabled} />
            ) : (
              <p className="mt-6 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900">
                Choose who you&apos;re signed in as (bottom of the sidebar) to reply.
              </p>
            ))}
        </section>

        <aside className="space-y-4">
          {aiEnabled && <AiPanel insights={detail.insights} refresh={refreshSummary.bind(null, c.id)} />}
          <form
            key={c.updatedAt}
            action={updateCase.bind(null, c.id, {
              status: c.status,
              priority: c.priority,
              category: c.category,
              assignedStaffId: c.assignedStaffId ?? "",
            })}
            className="rounded-xl border border-slate-200 bg-white p-4"
          >
            <h2 className="text-sm font-semibold text-slate-900">Case details</h2>
            <div className="mt-3 space-y-3 text-sm">
              <label className="block">
                <span className="text-xs text-slate-500">Status</span>
                <select name="status" defaultValue={c.status} className={field}>
                  {CASE_STATUSES.map((s) => (
                    <option key={s} value={s}>
                      {STATUS_LABELS[s]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="text-xs text-slate-500">Priority</span>
                <select name="priority" defaultValue={c.priority} className={field}>
                  {CASE_PRIORITIES.map((p) => (
                    <option key={p}>{p}</option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="text-xs text-slate-500">Category</span>
                <select name="category" defaultValue={c.category} className={field}>
                  {CASE_CATEGORIES.map((x) => (
                    <option key={x} value={x}>
                      {CATEGORY_LABELS[x]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="text-xs text-slate-500">Owner</span>
                <select name="assignedStaffId" defaultValue={c.assignedStaffId ?? ""} className={field}>
                  <option value="">Unassigned</option>
                  {staff.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name} · {s.team}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <button className="mt-4 w-full rounded-md bg-slate-900 py-1.5 text-sm font-medium text-white hover:bg-slate-700">
              Save changes
            </button>
          </form>

          <section className="rounded-xl border border-slate-200 bg-white p-4 text-sm">
            <h2 className="font-semibold text-slate-900">Customer</h2>
            <p className="mt-2 font-medium text-slate-800">{customer.fullName}</p>
            <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-slate-600">
              <dt className="text-slate-400">Account</dt>
              <dd>{customer.accountNumber}</dd>
              <dt className="text-slate-400">Plan</dt>
              <dd>{customer.plan}</dd>
              <dt className="text-slate-400">Address</dt>
              <dd>
                {customer.serviceAddress}, {customer.city}
              </dd>
              <dt className="text-slate-400">Balance</dt>
              <dd className={customer.balance > 300 ? "font-medium text-red-700" : ""}>{money(customer.balance)}</dd>
              <dt className="text-slate-400">Email</dt>
              <dd className="truncate">{customer.email}</dd>
              <dt className="text-slate-400">Phone</dt>
              <dd>{customer.phone}</dd>
              <dt className="text-slate-400">Since</dt>
              <dd>{new Date(customer.customerSince).getFullYear()}</dd>
            </dl>
          </section>

          <section className="rounded-xl border border-slate-200 bg-white p-4 text-sm">
            <h2 className="font-semibold text-slate-900">Other cases ({otherCases.length})</h2>
            <ul className="mt-2 space-y-2">
              {otherCases.map((o) => (
                <li key={o.id}>
                  <Link href={`/workspace/cases/${o.id}`} className="block hover:text-indigo-700">
                    {o.title}
                  </Link>
                  <span className="text-xs text-slate-400">
                    {o.reference} · {STATUS_LABELS[o.status]} · {relativeTime(o.updatedAt)}
                  </span>
                </li>
              ))}
              {otherCases.length === 0 && <li className="text-slate-400">None</li>}
            </ul>
          </section>
        </aside>
      </div>
    </div>
  );
}
