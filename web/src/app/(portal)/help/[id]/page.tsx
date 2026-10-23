import Link from "next/link";
import { notFound } from "next/navigation";
import { knowledgeApi } from "@/lib/api";
import { CATEGORY_LABELS } from "@/lib/types";

export default async function ArticlePage({ params }: PageProps<"/help/[id]">) {
  const { id } = await params;
  const article = await knowledgeApi.get(id);
  if (!article) notFound();

  return (
    <article className="mx-auto max-w-3xl px-4 py-10">
      <Link href="/help" className="text-sm text-slate-500 hover:text-slate-800">
        ← Help centre
      </Link>
      <p className="mt-6 text-xs font-medium uppercase tracking-wide text-emerald-700">{CATEGORY_LABELS[article.category]}</p>
      <h1 className="mt-1 text-2xl font-semibold text-slate-900">{article.title}</h1>
      <p className="mt-4 leading-7 text-slate-700">{article.body}</p>

      <div className="mt-10 rounded-xl bg-slate-50 p-5">
        <p className="font-medium text-slate-900">Still need help?</p>
        <p className="mt-1 text-sm text-slate-600">Start a request and our team will follow up.</p>
        <Link
          href={`/requests/new?category=${article.category}`}
          className="mt-3 inline-block rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700"
        >
          Start a request
        </Link>
      </div>
    </article>
  );
}
