import { Link, Navigate, Route, Routes } from 'react-router'
import { ErrorBoundary } from '@/components/error-boundary'
import { ClassDetailsPage } from '@/features/classes/class-details-page'
import { ClassesPage } from '@/features/classes/classes-page'
import { CreateClassPage } from '@/features/classes/create-class-page'

export default function App() {
  return (
    <div className="min-h-svh bg-background">
      <header className="border-b bg-card/80">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-4 sm:px-8">
          <Link to="/classes" className="font-semibold tracking-tight">School Admission</Link>
          <nav aria-label="Main navigation" className="flex items-center gap-5 text-sm">
            <Link to="/classes" className="text-muted-foreground hover:text-foreground">Classes</Link>
            <Link to="/admin" className="text-muted-foreground hover:text-foreground">Create class</Link>
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-5 py-10 sm:px-8 sm:py-14">
        <ErrorBoundary>
          <Routes>
            <Route path="/" element={<Navigate to="/classes" replace />} />
            <Route path="/classes" element={<ClassesPage />} />
            <Route path="/classes/:classId" element={<ClassDetailsPage />} />
            <Route path="/admin" element={<CreateClassPage />} />
            <Route path="*" element={<Navigate to="/classes" replace />} />
          </Routes>
        </ErrorBoundary>
      </main>
    </div>
  )
}
