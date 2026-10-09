function App() {
  // HU-12 negative check: a type error on purpose to prove that the frontend check blocks the merge.
  const title: string = 42

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-100 p-6">
      <div className="text-center">
        <h1 className="text-4xl font-bold text-slate-900">{title}</h1>
        <p className="mt-2 text-slate-600">Support ticket system</p>
      </div>
    </main>
  )
}

export default App
