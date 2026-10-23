export const CASE_STATUSES = ["New", "InProgress", "WaitingOnCustomer", "Resolved", "Closed"] as const;
export const CASE_PRIORITIES = ["Low", "Normal", "High", "Urgent"] as const;
export const CASE_CATEGORIES = ["Outage", "Billing", "Payment", "MoveService", "Meter", "General"] as const;

export type CaseStatus = (typeof CASE_STATUSES)[number];
export type CasePriority = (typeof CASE_PRIORITIES)[number];
export type CaseCategory = (typeof CASE_CATEGORIES)[number];
export type Channel = "Portal" | "Email" | "Chat" | "Phone" | "Sms";
export type AuthorType = "Customer" | "Staff" | "AiAgent" | "System";

export type Customer = {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  accountNumber: string;
  serviceAddress: string;
  city: string;
  plan: string;
  balance: number;
  customerSince: string;
};

export type Staff = { id: string; name: string; email: string; team: string };

export type CaseSummary = {
  id: string;
  reference: string;
  title: string;
  category: CaseCategory;
  priority: CasePriority;
  status: CaseStatus;
  channel: Channel;
  customerId: string;
  customerName: string;
  accountNumber: string;
  assignedStaffId: string | null;
  assignedStaffName: string | null;
  createdAt: string;
  updatedAt: string;
  firstResponseDueAt: string;
  resolutionDueAt: string;
  firstResponseBreached: boolean;
  resolutionBreached: boolean;
  sentiment: Sentiment | null;
  safetyRisk: boolean;
};

export type Sentiment = "Positive" | "Neutral" | "Negative" | "Distressed";

export type CaseInsights = {
  summary: string;
  sentiment: Sentiment;
  suggestedCategory: CaseCategory;
  suggestedPriority: CasePriority;
  confidence: number;
  safetyRisk: boolean;
  reason: string;
  applied: boolean;
  analyzedAt: string;
};

export type DraftReply = { reply: string; sources: string[] };

export type Message = {
  id: string;
  authorType: AuthorType;
  authorName: string;
  body: string;
  isInternal: boolean;
  createdAt: string;
};

export type CaseDetail = { summary: CaseSummary; customer: Customer; messages: Message[]; insights: CaseInsights | null };

export type Article = { id: string; title: string; category: CaseCategory; body: string; updatedAt: string };

export type CaseStats = {
  open: number;
  new: number;
  waitingOnCustomer: number;
  resolvedToday: number;
  firstResponseBreached: number;
  resolutionBreached: number;
  openByCategory: Partial<Record<CaseCategory, number>>;
};

export const CATEGORY_LABELS: Record<CaseCategory, string> = {
  Outage: "Power outage",
  Billing: "Billing",
  Payment: "Payments",
  MoveService: "Moving",
  Meter: "Meter",
  General: "General question",
};

export const STATUS_LABELS: Record<CaseStatus, string> = {
  New: "New",
  InProgress: "In progress",
  WaitingOnCustomer: "Waiting on customer",
  Resolved: "Resolved",
  Closed: "Closed",
};
