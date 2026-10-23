import { relativeTime } from "@/lib/format";
import type { CaseSummary } from "@/lib/types";

export function SlaIndicator({ c }: { c: CaseSummary }) {
  if (c.status === "Resolved" || c.status === "Closed") return <span className="text-slate-400">—</span>;
  if (c.firstResponseBreached || c.resolutionBreached) {
    return (
      <span className="font-medium text-red-700">
        {c.firstResponseBreached ? "Response overdue" : "Resolution overdue"}
      </span>
    );
  }
  return <span className="text-slate-600">Due {relativeTime(c.resolutionDueAt)}</span>;
}
