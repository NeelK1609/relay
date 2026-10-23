import { workspaceApi } from "@/lib/api";
import { signInAs } from "../actions";

export const metadata = { title: "Sign in" };

export default async function SignIn() {
  const customers = await workspaceApi.customers();

  return (
    <div className="mx-auto max-w-md px-4 py-12">
      <h1 className="text-2xl font-semibold text-slate-900">Sign in</h1>
      <p className="mt-2 text-sm text-slate-600">
        This demo uses sample customers. Pick one to see the portal from their point of view.
      </p>
      <form action={signInAs} className="mt-6 space-y-2">
        {customers.map((c) => (
          <button
            key={c.id}
            name="customerId"
            value={c.id}
            className="flex w-full items-center justify-between rounded-lg border border-slate-200 bg-white px-4 py-3 text-left hover:border-emerald-400"
          >
            <span>
              <span className="block font-medium text-slate-900">{c.fullName}</span>
              <span className="block text-sm text-slate-500">
                {c.accountNumber} · {c.city}
              </span>
            </span>
            <span className="text-sm text-emerald-700">Continue →</span>
          </button>
        ))}
      </form>
    </div>
  );
}
