import type { Metadata } from "next";
import Link from "next/link";
import { AutoSubmitSelect } from "@/components/auto-submit-select";
import { workspaceApi } from "@/lib/api";
import { getStaffId } from "@/lib/session";
import { switchStaff } from "./actions";

export const metadata: Metadata = { title: { default: "Workspace", template: "%s · Relay" } };

export default async function WorkspaceLayout({ children }: LayoutProps<"/workspace">) {
  const [staff, staffId] = await Promise.all([workspaceApi.staff(), getStaffId()]);
  const me = staff.find((s) => s.id === staffId);

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 lg:flex-row">
      <aside className="flex flex-col gap-6 bg-slate-900 px-4 py-5 text-slate-300 lg:w-60 lg:shrink-0">
        <div>
          <Link href="/workspace" className="text-lg font-semibold text-white">
            Relay
          </Link>
          <p className="text-xs text-slate-400">Highland Energy · Support</p>
        </div>
        <nav className="flex gap-1 text-sm lg:flex-col">
          <Link href="/workspace" className="rounded-md px-3 py-2 hover:bg-slate-800 hover:text-white">
            Queue
          </Link>
          <Link href={me ? `/workspace?assignedStaffId=${me.id}&openOnly=true` : "/workspace"} className="rounded-md px-3 py-2 hover:bg-slate-800 hover:text-white">
            My cases
          </Link>
          <Link href="/" className="rounded-md px-3 py-2 hover:bg-slate-800 hover:text-white">
            Customer portal ↗
          </Link>
        </nav>
        <form action={switchStaff} className="lg:mt-auto">
          <label htmlFor="staffId" className="text-xs uppercase tracking-wide text-slate-400">
            Signed in as (demo)
          </label>
          <AutoSubmitSelect
            id="staffId"
            name="staffId"
            defaultValue={me?.id ?? ""}
            className="mt-1 w-full rounded-md border border-slate-700 bg-slate-800 px-2 py-1.5 text-sm text-white"
          >
            <option value="" disabled>
              Choose a staff member
            </option>
            {staff.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} · {s.team}
              </option>
            ))}
          </AutoSubmitSelect>
          <noscript>
            <button className="mt-2 text-xs underline">Switch</button>
          </noscript>
        </form>
      </aside>
      <main className="min-w-0 flex-1">{children}</main>
    </div>
  );
}
