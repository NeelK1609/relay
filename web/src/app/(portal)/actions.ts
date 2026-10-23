"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { ApiError, portalApi } from "@/lib/api";
import { getCustomerId, setCustomerId } from "@/lib/session";
import { CASE_CATEGORIES, type CaseCategory } from "@/lib/types";

async function requireCustomer() {
  const id = await getCustomerId();
  if (!id) redirect("/sign-in");
  return id;
}

export async function signInAs(formData: FormData) {
  await setCustomerId(String(formData.get("customerId")));
  redirect("/requests");
}

export async function signOut() {
  await setCustomerId(null);
  redirect("/");
}

export async function openRequest(formData: FormData) {
  const customerId = await requireCustomer();
  const category = String(formData.get("category")) as CaseCategory;
  const title = String(formData.get("title") ?? "").trim();
  const description = String(formData.get("description") ?? "").trim();

  if (!CASE_CATEGORIES.includes(category) || !title || !description) {
    redirect(`/requests/new?category=${category}&error=${encodeURIComponent("Please fill in every field.")}`);
  }

  let caseId: string;
  try {
    caseId = (await portalApi.openCase(customerId, { title, description, category })).summary.id;
  } catch (e) {
    if (e instanceof ApiError && (e.status === 400 || e.status === 429)) {
      redirect(`/requests/new?category=${category}&error=${encodeURIComponent(e.message)}`);
    }
    throw e;
  }
  redirect(`/requests/${caseId}?created=1`);
}

export async function replyToRequest(caseId: string, formData: FormData) {
  const customerId = await requireCustomer();
  const body = String(formData.get("body") ?? "").trim();
  if (!body) return;
  try {
    await portalApi.reply(customerId, caseId, body);
  } catch (e) {
    if (e instanceof ApiError && e.status === 400) {
      redirect(`/requests/${caseId}?error=${encodeURIComponent(e.message)}`);
    }
    throw e;
  }
  revalidatePath(`/requests/${caseId}`);
}

type ChatResult<T> = { data?: T; error?: string };

async function chatCall<T>(fn: (customerId: string) => Promise<T>): Promise<ChatResult<T>> {
  const customerId = await getCustomerId();
  if (!customerId) return { error: "Sign in to chat with us." };
  try {
    return { data: await fn(customerId) };
  } catch (e) {
    return { error: e instanceof ApiError ? e.message : "Chat is unavailable right now. Please try again." };
  }
}

export async function startChat() {
  return chatCall((customerId) => portalApi.startChat(customerId));
}

export async function resumeChat(chatId: string) {
  return chatCall(async (customerId) => (await portalApi.connectChat(customerId, chatId)) ?? undefined);
}

export async function sendChat(chatId: string, text: string) {
  return chatCall((customerId) => portalApi.sendChat(customerId, chatId, text));
}

export async function endChat(chatId: string) {
  return chatCall((customerId) => portalApi.endChat(customerId, chatId));
}
