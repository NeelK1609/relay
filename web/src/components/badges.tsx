import { CATEGORY_LABELS, STATUS_LABELS, type CaseCategory, type CasePriority, type CaseStatus } from "@/lib/types";

const base = "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset whitespace-nowrap";

const statusStyles: Record<CaseStatus, string> = {
  New: "bg-sky-50 text-sky-700 ring-sky-200",
  InProgress: "bg-indigo-50 text-indigo-700 ring-indigo-200",
  WaitingOnCustomer: "bg-amber-50 text-amber-800 ring-amber-200",
  Resolved: "bg-emerald-50 text-emerald-700 ring-emerald-200",
  Closed: "bg-slate-100 text-slate-600 ring-slate-200",
};

const priorityStyles: Record<CasePriority, string> = {
  Urgent: "bg-red-50 text-red-700 ring-red-200",
  High: "bg-orange-50 text-orange-700 ring-orange-200",
  Normal: "bg-slate-50 text-slate-700 ring-slate-200",
  Low: "bg-slate-50 text-slate-500 ring-slate-200",
};

export function StatusBadge({ status }: { status: CaseStatus }) {
  return <span className={`${base} ${statusStyles[status]}`}>{STATUS_LABELS[status]}</span>;
}

export function PriorityBadge({ priority }: { priority: CasePriority }) {
  return <span className={`${base} ${priorityStyles[priority]}`}>{priority}</span>;
}

export function CategoryBadge({ category }: { category: CaseCategory }) {
  return <span className={`${base} bg-white text-slate-600 ring-slate-200`}>{CATEGORY_LABELS[category]}</span>;
}
