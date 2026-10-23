"use client";

import { useEffect, useState } from "react";
import type { ChatConnection } from "@/lib/types";

export type LiveMessage = { id: string; senderId: string; senderName: string; text: string; at: string };

/**
 * Loads a chat thread's history and streams new messages in real time from Azure Communication Services.
 * Sending goes through the Relay API (so the assistant can reply and the case stays in sync), not this hook.
 */
export function useLiveChat(connection: ChatConnection | null) {
  const [messages, setMessages] = useState<LiveMessage[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!connection) return;
    let stopped = false;
    let cleanup: (() => Promise<void>) | undefined;

    (async () => {
      try {
        // Loaded lazily: the SDK is browser-only and only needed once a chat is open.
        const [{ ChatClient }, { AzureCommunicationTokenCredential, getIdentifierRawId }] = await Promise.all([
          import("@azure/communication-chat"),
          import("@azure/communication-common"),
        ]);
        const client = new ChatClient(connection.endpoint, new AzureCommunicationTokenCredential(connection.token));
        const thread = client.getChatThreadClient(connection.threadId);

        const add = (m: LiveMessage) =>
          setMessages((list) => (list.some((x) => x.id === m.id) ? list : [...list, m].sort((a, b) => a.at.localeCompare(b.at))));

        // Newest 50 messages; merged by id, so repeated syncs are harmless.
        const sync = async () => {
          let seen = 0;
          for await (const m of thread.listMessages({ maxPageSize: 50 })) {
            if (stopped || ++seen > 50) break;
            if (m.type !== "text" || !m.content?.message || !m.sender) continue;
            add({
              id: m.id,
              senderId: getIdentifierRawId(m.sender),
              senderName: m.senderDisplayName ?? "",
              text: m.content.message,
              at: new Date(m.createdOn).toISOString(),
            });
          }
        };

        // The SDK requires starting notifications before subscribing to events.
        await client.startRealtimeNotifications();
        client.on("chatMessageReceived", (e) => {
          if (e.threadId !== connection.threadId || e.type.toLowerCase() !== "text") return;
          add({
            id: e.id,
            senderId: getIdentifierRawId(e.sender),
            senderName: e.senderDisplayName,
            text: e.message,
            at: new Date(e.createdOn).toISOString(),
          });
        });
        // Push registration finishes a moment after start; anything sent in that gap is caught by a resync,
        // and a slow poll covers dropped connections.
        client.on("realTimeNotificationConnected", () => void sync().catch(() => {}));
        const poll = setInterval(() => void sync().catch(() => {}), 5000);
        cleanup = async () => {
          clearInterval(poll);
          await client.stopRealtimeNotifications();
        };

        await sync();
      } catch (e) {
        console.error("Live chat connection failed", e);
        setError("Couldn't connect to chat. Refresh to try again.");
      }
    })();

    return () => {
      stopped = true;
      void cleanup?.();
    };
  }, [connection]);

  return { messages, error };
}
