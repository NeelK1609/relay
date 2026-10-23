import type {
  Article, CaseCategory, CaseDetail, CasePriority, CaseStats, CaseStatus, CaseSummary, Customer, Staff,
} from "./types";

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

function baseUrl() {
  const url = process.env.RELAY_API_URL;
  if (!url) {
    throw new Error("RELAY_API_URL is not set. Start everything with: dotnet run --project src/Relay.AppHost");
  }
  return url;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(baseUrl() + path, {
    ...init,
    headers: { "content-type": "application/json", ...init?.headers },
    cache: "no-store",
  });
  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = await res.json();
      message = typeof body === "string" ? body : (body.detail ?? body.title ?? message);
    } catch {}
    throw new ApiError(res.status, message);
  }
  return res.json() as Promise<T>;
}

async function maybe<T>(promise: Promise<T>): Promise<T | null> {
  try {
    return await promise;
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) return null;
    throw e;
  }
}

const json = (body: unknown) => JSON.stringify(body);
const query = (params: Record<string, string | undefined>) => {
  const q = new URLSearchParams(Object.entries(params).filter((e): e is [string, string] => !!e[1]));
  return q.size ? `?${q}` : "";
};

export const portalApi = {
  customer: (customerId: string) => maybe(request<Customer>(`/api/portal/customers/${customerId}`)),
  cases: (customerId: string) => request<CaseSummary[]>(`/api/portal/customers/${customerId}/cases`),
  getCase: (customerId: string, caseId: string) =>
    maybe(request<CaseDetail>(`/api/portal/customers/${customerId}/cases/${caseId}`)),
  openCase: (customerId: string, body: { title: string; description: string; category: CaseCategory }) =>
    request<CaseDetail>(`/api/portal/customers/${customerId}/cases`, { method: "POST", body: json(body) }),
  reply: (customerId: string, caseId: string, body: string) =>
    request<CaseDetail>(`/api/portal/customers/${customerId}/cases/${caseId}/messages`, {
      method: "POST",
      body: json({ body }),
    }),
};

export type CaseFilters = {
  status?: string;
  priority?: string;
  category?: string;
  assignedStaffId?: string;
  openOnly?: string;
  search?: string;
};

export const workspaceApi = {
  staff: () => request<Staff[]>("/api/workspace/staff"),
  customers: () => request<Customer[]>("/api/workspace/customers"),
  stats: () => request<CaseStats>("/api/workspace/stats"),
  cases: (filters: CaseFilters) => request<CaseSummary[]>(`/api/workspace/cases${query(filters)}`),
  getCase: (caseId: string) => maybe(request<CaseDetail>(`/api/workspace/cases/${caseId}`)),
  updateCase: (
    caseId: string,
    body: { status?: CaseStatus; priority?: CasePriority; category?: CaseCategory; assignedStaffId?: string; unassign?: boolean },
  ) => request<CaseDetail>(`/api/workspace/cases/${caseId}`, { method: "PATCH", body: json(body) }),
  postMessage: (caseId: string, body: { staffId: string; body: string; isInternal: boolean }) =>
    request<CaseDetail>(`/api/workspace/cases/${caseId}/messages`, { method: "POST", body: json(body) }),
};

export const knowledgeApi = {
  search: (q?: string, category?: string) => request<Article[]>(`/api/knowledge${query({ q, category })}`),
  get: (id: string) => maybe(request<Article>(`/api/knowledge/${id}`)),
};
