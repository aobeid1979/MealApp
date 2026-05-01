export default function RecipesLoading() {
  return (
    <section className="page-section">
      <div className="flex min-h-[55vh] items-center justify-center">
        <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mx-auto h-10 w-10 animate-spin rounded-full border-4 border-emerald-100 border-t-emerald-600" />
          <p className="mt-4 text-center text-sm font-medium text-slate-700">
            Loading recipes...
          </p>
          <div className="mt-6 space-y-3">
            <div className="h-4 animate-pulse rounded bg-slate-100" />
            <div className="h-4 w-5/6 animate-pulse rounded bg-slate-100" />
            <div className="h-4 w-2/3 animate-pulse rounded bg-slate-100" />
          </div>
        </div>
      </div>
    </section>
  );
}
