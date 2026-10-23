"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import Link from "next/link";
import type { ChatConnection, ChatState, ChatStatus } from "@/lib/types";
import { useLiveChat } from "./use-live-chat";

type Result<T> = { data?: T; error?: string };

type Actions = {
  start: () => Promise<Result<ChatConnection>>;
  resume: (chatId: string) => Promise<Result<ChatConnection | undefined>>;
  send: (chatId: string, text: string) => Promise<Result<ChatState>>;
  end: (chatId: string) => Promise<Result<void>>;
};

const STORAGE_KEY = "relay-chat-session";

const STATUS_TEXT: Record<ChatStatus, string> = {
  WithAssistant: "Chatting with Relay Assistant (AI)",
  WaitingForAgent: "Waiting for a team member…",
  WithAgent: "Chatting with our team",
  Ended: "Chat ended",
};

function readStoredSession() {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function storeSession(id: string | null) {
  try {
    if (id) sessionStorage.setItem(STORAGE_KEY, id);
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {}
}

export function ChatWidget(actions: Actions) {
  const [open, setOpen] = useState(false);
  const [connection, setConnection] = useState<ChatConnection | null>(null);
  const [error, setError] = useState<string>();
  const [pending, start] = useTransition();

  const openChat = () => {
    setOpen(true);
    if (connection) return;
    start(async () => {
      setError(undefined);
      const stored = readStoredSession();
      const resumed = stored ? await actions.resume(stored) : undefined;
      const result = resumed?.data ? resumed : await actions.start();
      if (result.error || !result.data) {
        setError(result.error ?? "Chat is unavailable right now.");
        return;
      }
      storeSession(result.data.sessionId);
      setConnection(result.data);
    });
  };

  const startOver = () => {
    storeSession(null);
    setConnection(null);
    start(async () => {
      const result = await actions.start();
      if (result.error || !result.data) setError(result.error ?? "Chat is unavailable right now.");
      else {
        storeSession(result.data.sessionId);
        setConnection(result.data);
      }
    });
  };

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col items-end gap-3">
      {open && (
        <section
          aria-label="Chat with Highland Energy"
          className="flex h-[32rem] max-h-[80vh] w-[calc(100vw-2rem)] max-w-sm flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl"
        >
          <header className="flex items-center justify-between bg-emerald-600 px-4 py-3 text-white">
            <div>
              <p className="font-semibold">Chat with us</p>
              <p className="text-xs text-emerald-100">{connection ? STATUS_TEXT[connection.status] : "Connecting…"}</p>
            </div>
            <button onClick={() => setOpen(false)} aria-label="Minimize chat" className="rounded-md px-2 py-1 hover:bg-emerald-700">
              ✕
            </button>
          </header>
          {connection ? (
            <ChatBody key={connection.sessionId} connection={connection} actions={actions} onUpdate={setConnection} onRestart={startOver} />
          ) : (
            <div className="grid flex-1 place-items-center p-6 text-center text-sm text-slate-600">
              {error ? (
                <p role="alert" className="text-red-700">
                  {error}
                </p>
              ) : (
                <p>{pending ? "Connecting to chat…" : ""}</p>
              )}
            </div>
          )}
        </section>
      )}
      {!open && (
        <button
          onClick={openChat}
          className="rounded-full bg-emerald-600 px-5 py-3 font-medium text-white shadow-lg hover:bg-emerald-700"
        >
          💬 Chat with us
        </button>
      )}
    </div>
  );
}

function ChatBody({
  connection,
  actions,
  onUpdate,
  onRestart,
}: {
  connection: ChatConnection;
  actions: Actions;
  onUpdate: (c: ChatConnection) => void;
  onRestart: () => void;
}) {
  const { messages, error: liveError } = useLiveChat(connection);
  const [text, setText] = useState("");
  const [error, setError] = useState<string>();
  const [waiting, startSend] = useTransition();
  const bottom = useRef<HTMLDivElement>(null);
  const ended = connection.status === "Ended";

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "end" });
  }, [messages.length, waiting]);

  const send = () => {
    const message = text.trim();
    if (!message || waiting) return;
    setText("");
    startSend(async () => {
      setError(undefined);
      const result = await actions.send(connection.sessionId, message);
      if (result.error || !result.data) setError(result.error ?? "Your message wasn't sent.");
      else onUpdate({ ...connection, status: result.data.status, caseId: result.data.caseId, caseReference: result.data.caseReference });
    });
  };

  const end = () =>
    startSend(async () => {
      await actions.end(connection.sessionId);
      onUpdate({ ...connection, status: "Ended" });
    });

  return (
    <>
      <ol className="flex-1 space-y-3 overflow-y-auto bg-slate-50 px-3 py-4 text-sm" aria-live="polite">
        {messages.map((m) => {
          const mine = m.senderId === connection.userId;
          const ai = m.senderName === "Relay Assistant";
          return (
            <li key={m.id} className={`flex ${mine ? "justify-end" : "justify-start"}`}>
              <div
                className={`max-w-[85%] rounded-2xl px-3 py-2 ${
                  mine ? "bg-emerald-600 text-white" : ai ? "border border-violet-200 bg-violet-50" : "border border-slate-200 bg-white"
                }`}
              >
                {!mine && (
                  <p className="text-xs text-slate-500">
                    {m.senderName}
                    {ai && " (AI)"}
                  </p>
                )}
                <p className="whitespace-pre-wrap leading-5">{m.text}</p>
              </div>
            </li>
          );
        })}
        {waiting && connection.status === "WithAssistant" && <li className="text-xs text-slate-500">Relay Assistant is typing…</li>}
        <div ref={bottom} />
      </ol>

      {connection.caseId && (
        <p className="border-t border-slate-200 bg-white px-3 py-2 text-xs text-slate-600">
          Case{" "}
          <Link href={`/requests/${connection.caseId}`} className="font-medium text-emerald-700 hover:underline">
            {connection.caseReference}
          </Link>{" "}
          is open for this chat.
        </p>
      )}
      {(error || liveError) && (
        <p role="alert" className="bg-red-50 px-3 py-2 text-xs text-red-800">
          {error ?? liveError}
        </p>
      )}

      {ended ? (
        <div className="border-t border-slate-200 p-3 text-center">
          <button onClick={onRestart} className="text-sm font-medium text-emerald-700 hover:underline">
            Start a new chat
          </button>
        </div>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            send();
          }}
          className="flex gap-2 border-t border-slate-200 p-3"
        >
          <label htmlFor="chat-input" className="sr-only">
            Message
          </label>
          <input
            id="chat-input"
            value={text}
            onChange={(e) => setText(e.target.value)}
            maxLength={1000}
            placeholder="Type a message…"
            className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-emerald-500 focus:ring-2 focus:ring-emerald-200"
          />
          <button disabled={waiting} className="rounded-lg bg-emerald-600 px-3 text-sm font-medium text-white disabled:opacity-60">
            Send
          </button>
          <button type="button" onClick={end} className="text-xs text-slate-500 hover:text-slate-800">
            End
          </button>
        </form>
      )}
    </>
  );
}
