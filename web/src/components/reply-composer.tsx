"use client";

import { useState, useTransition } from "react";

type Draft = { reply?: string; sources?: string[]; error?: string };

export function ReplyComposer({
  action,
  draft,
  aiEnabled,
}: {
  action: (formData: FormData) => Promise<void>;
  draft: () => Promise<Draft>;
  aiEnabled: boolean;
}) {
  const [body, setBody] = useState("");
  const [sources, setSources] = useState<string[]>([]);
  const [error, setError] = useState<string>();
  const [drafting, startDraft] = useTransition();

  const onDraft = () =>
    startDraft(async () => {
      setError(undefined);
      const result = await draft();
      if (result.error) setError(result.error);
      else {
        setBody(result.reply ?? "");
        setSources(result.sources ?? []);
      }
    });

  return (
    <form
      action={async (fd) => {
        await action(fd);
        setBody("");
        setSources([]);
      }}
      className="mt-6 rounded-xl border border-slate-200 bg-white p-4"
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        <label htmlFor="body" className="text-sm font-medium text-slate-800">
          Message
        </label>
        {aiEnabled && (
          <button
            type="button"
            onClick={onDraft}
            disabled={drafting}
            className="rounded-md border border-violet-200 bg-violet-50 px-3 py-1 text-sm font-medium text-violet-800 hover:bg-violet-100 disabled:opacity-60"
          >
            {drafting ? "Drafting…" : "✨ Draft with AI"}
          </button>
        )}
      </div>
      <textarea
        id="body"
        name="body"
        required
        rows={7}
        maxLength={4000}
        value={body}
        onChange={(e) => setBody(e.target.value)}
        placeholder="Write a reply to the customer, or an internal note for the team"
        className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-200"
      />
      {error && (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      )}
      {sources.length > 0 && (
        <p className="mt-2 text-xs text-violet-800">
          AI draft based on: {sources.join("; ")}. Check it and fill in any [placeholders] before sending.
        </p>
      )}
      <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
        <fieldset className="flex gap-3">
          <legend className="sr-only">Visibility</legend>
          <label className="flex items-center gap-1.5">
            <input type="radio" name="visibility" value="public" defaultChecked /> Reply to customer
          </label>
          <label className="flex items-center gap-1.5">
            <input type="radio" name="visibility" value="internal" /> Internal note
          </label>
        </fieldset>
        <select name="thenStatus" aria-label="Then set status" className="rounded-md border border-slate-300 px-2 py-1">
          <option value="">Keep status</option>
          <option value="WaitingOnCustomer">Then: waiting on customer</option>
          <option value="Resolved">Then: resolved</option>
        </select>
        <button className="ml-auto rounded-md bg-indigo-600 px-4 py-1.5 font-medium text-white hover:bg-indigo-700">Send</button>
      </div>
    </form>
  );
}
