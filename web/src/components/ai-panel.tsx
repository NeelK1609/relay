import { relativeTime } from "@/lib/format";
import { CATEGORY_LABELS, type CaseInsights, type Sentiment } from "@/lib/types";

const sentimentStyle: Record<Sentiment, string> = {
  Positive: "bg-emerald-100 text-emerald-800",
  Neutral: "bg-slate-100 text-slate-700",
  Negative: "bg-orange-100 text-orange-800",
  Distressed: "bg-red-100 text-red-800",
};

export function SentimentBadge({ sentiment }: { sentiment: Sentiment }) {
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${sentimentStyle[sentiment]}`}>{sentiment}</span>;
}

export function AiPanel({ insights, refresh }: { insights: CaseInsights | null; refresh: () => Promise<void> }) {
  return (
    <section className="rounded-xl border border-violet-200 bg-violet-50/60 p-4 text-sm">
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-semibold text-violet-950">AI insights</h2>
        <form action={refresh}>
          <button className="text-xs font-medium text-violet-800 hover:underline">Refresh summary</button>
        </form>
      </div>

      {insights ? (
        <>
          {insights.safetyRisk && (
            <p className="mt-2 rounded-md bg-red-100 px-2 py-1 text-xs font-medium text-red-800">⚠ Possible safety risk</p>
          )}
          <p className="mt-2 leading-6 text-slate-800">{insights.summary}</p>
          <dl className="mt-3 grid grid-cols-[auto_1fr] items-center gap-x-3 gap-y-1.5 text-slate-600">
            <dt className="text-slate-500">Sentiment</dt>
            <dd>
              <SentimentBadge sentiment={insights.sentiment} />
            </dd>
            {insights.confidence > 0 && (
              <>
                <dt className="text-slate-500">Triage</dt>
                <dd>
                  {CATEGORY_LABELS[insights.suggestedCategory]}, {insights.suggestedPriority}
                  <span className="text-slate-400"> · {Math.round(insights.confidence * 100)}%</span>
                  <span className={`ml-1 text-xs ${insights.applied ? "text-emerald-700" : "text-amber-700"}`}>
                    {insights.applied ? "applied" : "suggested"}
                  </span>
                </dd>
              </>
            )}
          </dl>
          <p className="mt-3 text-xs text-slate-400">Updated {relativeTime(insights.analyzedAt)}</p>
        </>
      ) : (
        <p className="mt-2 text-slate-600">Not analysed yet. New cases are triaged automatically within a few seconds.</p>
      )}
    </section>
  );
}
