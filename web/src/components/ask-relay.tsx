"use client";

import { useState, useTransition } from "react";

type Turn = { question: string; answer?: string; toolsUsed?: string[]; error?: string };

const SUGGESTIONS = [
  "Which urgent cases are unassigned?",
  "Summarize Emily Chartrand's open cases",
  "What's breaching SLA right now?",
];

export function AskRelay({ ask }: { ask: (q: string) => Promise<Omit<Turn, "question">> }) {
  const [question, setQuestion] = useState("");
  const [turns, setTurns] = useState<Turn[]>([]);
  const [pending, start] = useTransition();

  const submit = (q: string) => {
    const text = q.trim();
    if (!text || pending) return;
    setQuestion("");
    setTurns((t) => [...t, { question: text }]);
    start(async () => {
      const result = await ask(text);
      setTurns((t) => t.map((turn, i) => (i === t.length - 1 ? { ...turn, ...result } : turn)));
    });
  };

  return (
    <section aria-label="Ask Relay" className="mt-6 rounded-xl border border-violet-200 bg-white">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-violet-100 px-4 py-3">
        <h2 className="font-semibold text-violet-950">✨ Ask Relay</h2>
        <span className="text-xs text-slate-500">AI assistant using the Relay CRM tools over MCP</span>
      </div>

      {turns.length > 0 && (
        <ol className="max-h-96 space-y-4 overflow-y-auto px-4 py-4 text-sm">
          {turns.map((t, i) => (
            <li key={i} className="space-y-2">
              <p className="font-medium text-slate-900">{t.question}</p>
              {t.error ? (
                <p role="alert" className="text-red-700">
                  {t.error}
                </p>
              ) : t.answer ? (
                <div className="rounded-lg bg-violet-50 px-3 py-2">
                  <p className="whitespace-pre-wrap leading-6 text-slate-800">{t.answer}</p>
                  {t.toolsUsed && t.toolsUsed.length > 0 && (
                    <p className="mt-2 text-xs text-violet-700">Tools: {t.toolsUsed.join(", ")}</p>
                  )}
                </div>
              ) : (
                <p className="text-slate-500">Looking that up…</p>
              )}
            </li>
          ))}
        </ol>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          submit(question);
        }}
        className="flex flex-wrap gap-2 border-t border-violet-100 px-4 py-3"
      >
        <label htmlFor="ask" className="sr-only">
          Ask a question about the queue
        </label>
        <input
          id="ask"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          maxLength={1000}
          placeholder="Ask about cases, customers or the queue…"
          className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-violet-500 focus:ring-2 focus:ring-violet-200"
        />
        <button
          disabled={pending}
          className="rounded-md bg-violet-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-60"
        >
          Ask
        </button>
        {turns.length === 0 && (
          <div className="flex w-full flex-wrap gap-2 pt-1">
            {SUGGESTIONS.map((s) => (
              <button
                key={s}
                type="button"
                onClick={() => submit(s)}
                className="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-600 hover:border-violet-300 hover:text-violet-800"
              >
                {s}
              </button>
            ))}
          </div>
        )}
      </form>
    </section>
  );
}
