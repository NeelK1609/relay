import Link from "next/link";
import { portalApi } from "@/lib/api";
import { getCustomerId } from "@/lib/session";
import { ChatWidget } from "@/components/chat-widget";
import { endChat, resumeChat, sendChat, signOut, startChat } from "./actions";

export default async function PortalLayout({ children }: LayoutProps<"/">) {
  const customerId = await getCustomerId();
  const customer = customerId ? await portalApi.customer(customerId) : null;

  return (
    <div className="flex min-h-screen flex-col bg-white">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-4">
          <Link href="/" className="flex items-center gap-2 font-semibold text-slate-900">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-emerald-600 text-white" aria-hidden>
              ⚡
            </span>
            Highland Energy
          </Link>
          <nav className="flex items-center gap-1 text-sm">
            <Link href="/help" className="rounded-md px-3 py-2 text-slate-600 hover:bg-slate-100 hover:text-slate-900">
              Help centre
            </Link>
            <Link href="/requests" className="rounded-md px-3 py-2 text-slate-600 hover:bg-slate-100 hover:text-slate-900">
              My requests
            </Link>
            {customer ? (
              <form action={signOut} className="flex items-center gap-2 pl-2">
                <span className="hidden text-slate-500 sm:inline">{customer.fullName}</span>
                <button className="rounded-md border border-slate-200 px-3 py-1.5 text-slate-700 hover:bg-slate-50">
                  Sign out
                </button>
              </form>
            ) : (
              <Link
                href="/sign-in"
                className="ml-2 rounded-md bg-emerald-600 px-3 py-1.5 font-medium text-white hover:bg-emerald-700"
              >
                Sign in
              </Link>
            )}
          </nav>
        </div>
      </header>
      <main className="flex-1">{children}</main>
      {customer && <ChatWidget start={startChat} resume={resumeChat} send={sendChat} end={endChat} />}
      <footer className="border-t border-slate-200 bg-slate-50">
        <div className="mx-auto flex max-w-5xl flex-wrap justify-between gap-2 px-4 py-6 text-sm text-slate-500">
          <span>Highland Energy is a fictional utility used to demo Relay.</span>
          <Link href="/workspace" className="hover:text-slate-800">
            Staff workspace →
          </Link>
        </div>
      </footer>
    </div>
  );
}
