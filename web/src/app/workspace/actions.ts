"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { ApiError, workspaceApi } from "@/lib/api";
import { getStaffId, setStaffId } from "@/lib/session";
import {
  CASE_CATEGORIES, CASE_PRIORITIES, CASE_STATUSES, type CaseCategory, type CasePriority, type CaseStatus,
} from "@/lib/types";

export async function switchStaff(formData: FormData) {
  await setStaffId(String(formData.get("staffId")));
  revalidatePath("/workspace", "layout");
}

async function run(caseId: string, action: () => Promise<unknown>) {
  try {
    await action();
  } catch (e) {
    if (e instanceof ApiError && e.status === 400) {
      redirect(`/workspace/cases/${caseId}?error=${encodeURIComponent(e.message)}`);
    }
    throw e;
  }
  revalidatePath(`/workspace/cases/${caseId}`);
}

type CaseFields = { status: string; priority: string; category: string; assignedStaffId: string };

export async function updateCase(caseId: string, current: CaseFields, formData: FormData) {
  // Only send what changed, so stale values (e.g. status New after an assignment) don't fight each other.
  const changed = <T extends string>(name: keyof CaseFields, allowed: readonly T[]) => {
    const v = formData.get(name);
    return typeof v === "string" && v !== current[name] && (allowed as readonly string[]).includes(v) ? (v as T) : undefined;
  };
  const assignee = String(formData.get("assignedStaffId") ?? "");
  const assigneeChanged = assignee !== current.assignedStaffId;

  await run(caseId, () =>
    workspaceApi.updateCase(caseId, {
      status: changed<CaseStatus>("status", CASE_STATUSES),
      priority: changed<CasePriority>("priority", CASE_PRIORITIES),
      category: changed<CaseCategory>("category", CASE_CATEGORIES),
      assignedStaffId: assigneeChanged && assignee ? assignee : undefined,
      unassign: assigneeChanged && !assignee,
    }),
  );
}

export async function draftReply(caseId: string): Promise<{ reply?: string; sources?: string[]; error?: string }> {
  const staffId = await getStaffId();
  if (!staffId) return { error: "Choose who you're signed in as first." };
  try {
    return await workspaceApi.draftReply(caseId, staffId);
  } catch (e) {
    return { error: e instanceof ApiError ? e.message : "The assistant couldn't draft a reply. Try again." };
  }
}

export async function askRelay(question: string): Promise<{ answer?: string; toolsUsed?: string[]; error?: string }> {
  const staffId = await getStaffId();
  if (!staffId) return { error: "Choose who you're signed in as first (bottom of the sidebar)." };
  try {
    const result = await workspaceApi.ask(staffId, question);
    revalidatePath("/workspace", "layout");
    return result;
  } catch (e) {
    return { error: e instanceof ApiError ? e.message : "Relay couldn't answer that. Try again." };
  }
}

export async function refreshSummary(caseId: string) {
  await run(caseId, () => workspaceApi.summarize(caseId));
}

export async function postMessage(caseId: string, formData: FormData) {
  const staffId = await getStaffId();
  if (!staffId) redirect("/workspace");
  const body = String(formData.get("body") ?? "").trim();
  if (!body) return;
  const isInternal = formData.get("visibility") === "internal";
  const nextStatus = formData.get("thenStatus");

  await run(caseId, async () => {
    await workspaceApi.postMessage(caseId, { staffId, body, isInternal });
    if (nextStatus === "WaitingOnCustomer" || nextStatus === "Resolved") {
      await workspaceApi.updateCase(caseId, { status: nextStatus });
    }
  });
}
