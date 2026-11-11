import { Link, Navigate, Route, Routes } from 'react-router'
import { ErrorBoundary } from '@/components/error-boundary'
import { ClassDetailsPage } from '@/features/classes/class-details-page'
import { ClassesPage } from '@/features/classes/classes-page'
import { CreateClassPage } from '@/features/classes/create-class-page'
import { HoldPage } from '@/features/holds/hold-page'
import { MyApplicationsPage } from '@/features/holds/my-applications-page'
import { useStudentId } from '@/lib/student-id'

export default function App() {
  const { studentId } = useStudentId()

  return (
    <div className="min-h-svh bg-background">
      <header className="border-b bg-card/90 backdrop-blur-sm sticky top-0 z-20">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-3.5 sm:px-8">
          <Link to="/classes" className="flex items-center gap-2">
            <span className="text-xl font-bold tracking-tight text-foreground">
              বিদ্যালয় ভর্তি <span className="text-xs font-normal text-muted-foreground">(Admission)</span>
            </span>
          </Link>
          <nav aria-label="Main navigation" className="flex items-center gap-4 sm:gap-6 text-sm font-medium">
            <Link to="/classes" className="text-muted-foreground hover:text-foreground transition-colors">
              ক্লাসসমূহ (Classes)
            </Link>
            <Link to="/me/holds" className="text-muted-foreground hover:text-foreground transition-colors">
              আমার আবেদন (My Applications)
              {Boolean(studentId) && (
                <span className="ml-1.5 inline-block size-2 rounded-full bg-emerald-500" title="Student ID active" />
              )}
            </Link>
            <Link
              to="/admin"
              className="rounded-lg border px-3 py-1.5 text-xs font-semibold text-foreground hover:bg-muted/60 transition-colors"
            >
              প্রশাসন (Admin)
            </Link>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-5 py-8 sm:px-8 sm:py-12">
        <ErrorBoundary>
          <Routes>
            <Route path="/" element={<Navigate to="/classes" replace />} />
            <Route path="/classes" element={<ClassesPage />} />
            <Route path="/classes/:classId" element={<ClassDetailsPage />} />
            <Route path="/holds/:holdId" element={<HoldPage />} />
            <Route path="/me/holds" element={<MyApplicationsPage />} />
            <Route path="/admin" element={<CreateClassPage />} />
            <Route path="*" element={<Navigate to="/classes" replace />} />
          </Routes>
        </ErrorBoundary>
      </main>
    </div>
  )
}
