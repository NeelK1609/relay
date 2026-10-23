"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import type { ChatConnection, ChatState } from "@/lib/types";
import { useLiveChat } from "./use-live-chat";

type Result<T> = { data?: T; error?: string };

/** Staff side of a live chat: join the customer's conversation and reply in real time. */
export function LiveChatPanel({
  state,
  join,
  send,
  end,
}: {
  state: ChatState;
  join: () => Promise<Result<ChatConnection>>;
  send: (text: string) => Promise<Result<void>>;
  end: () => Promise<Result<void>>;
}) {
  const [connection, setConnection] = useState<ChatConnection | null>(null);
  const [error, setError] = useState<string>();
  const [pending, start] = useTransition();

  if (state.status === "Ended") return null;

  const onJoin = () =>
    start(async () => {
      setError(undefined);
      const result = await join();
      if (result.error || !result.data) setError(result.error ?? "Couldn't join the chat.");
      else setConnection(result.data);
    });

  return (
    <section aria-label="Live chat" className="mt-6 overflow-hidden rounded-xl border border-emerald-200 bg-white">
      <header className="flex flex-wrap items-center justify-between gap-2 bg-emerald-50 px-4 py-3">
        <div>
          <h2 className="font-semibold text-emerald-950">💬 Live chat</h2>
          <p className="text-xs text-emerald-800">
            {state.status === "WaitingForAgent"
              ? "The customer is waiting for a team member."
              : `With ${state.staffName ?? "a team member"}.`}
          </p>
        </div>
        {!connection && (
          <button
            onClick={onJoin}
            disabled={pending}
            className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-60"
          >
            {pending ? "Joining…" : state.status === "WaitingForAgent" ? "Join chat" : "Open chat"}
          </button>
        )}
      </header>
      {error && (
        <p role="alert" className="px-4 py-2 text-sm text-red-700">
          {error}
        </p>
      )}
      {connection && <StaffChat key={connection.sessionId} connection={connection} send={send} end={end} />}
    </section>
  );
}

function StaffChat({
  connection,
  send,
  end,
}: {
  connection: ChatConnection;
  send: (text: string) => Promise<Result<void>>;
  end: () => Promise<Result<void>>;
}) {
  const { messages, error: liveError } = useLiveChat(connection);
  const [text, setText] = useState("");
  const [error, setError] = useState<string>();
  const [pending, start] = useTransition();
  const bottom = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "end" });
  }, [messages.length]);

  const onSend = () => {
    const message = text.trim();
    if (!message) return;
    setText("");
    start(async () => {
      const result = await send(message);
      setError(result.error);
    });
  };

  return (
    <>
      <ol className="max-h-80 space-y-2 overflow-y-auto bg-slate-50 px-4 py-3 text-sm" aria-live="polite">
        {messages.map((m) => {
          const mine = m.senderId === connection.userId;
          return (
            <li key={m.id} className={`flex ${mine ? "justify-end" : "justify-start"}`}>
              <div className={`max-w-[80%] rounded-xl px-3 py-2 ${mine ? "bg-indigo-600 text-white" : "border border-slate-200 bg-white"}`}>
                {!mine && <p className="text-xs text-slate-500">{m.senderName}</p>}
                <p className="whitespace-pre-wrap">{m.text}</p>
              </div>
            </li>
          );
        })}
        <div ref={bottom} />
      </ol>
      {(error || liveError) && (
        <p role="alert" className="px-4 py-2 text-sm text-red-700">
          {error ?? liveError}
        </p>
      )}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSend();
        }}
        className="flex gap-2 border-t border-slate-200 p-3"
      >
        <label htmlFor="staff-chat" className="sr-only">
          Chat message
        </label>
        <input
          id="staff-chat"
          value={text}
          onChange={(e) => setText(e.target.value)}
          maxLength={1000}
          placeholder="Message the customer…"
          className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-200"
        />
        <button disabled={pending} className="rounded-md bg-indigo-600 px-3 text-sm font-medium text-white disabled:opacity-60">
          Send
        </button>
        <button
          type="button"
          onClick={() => start(async () => void (await end()))}
          className="text-xs text-slate-500 hover:text-slate-800"
        >
          End chat
        </button>
      </form>
    </>
  );
}
