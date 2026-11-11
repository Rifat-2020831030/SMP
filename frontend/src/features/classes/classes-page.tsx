import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { useStudentId } from '@/lib/student-id'
import { useClassesStream } from '@/lib/use-classes-stream'
import { getMyHolds } from '@/features/holds/holds-api'
import type { StudentHold } from '@/features/holds/contracts'
import type { ClassSummary } from './contracts'
import { classesKey, listClasses } from './classes-api'

function formatCountdown(totalSeconds: number): string {
  const mins = Math.floor(totalSeconds / 60)
  const secs = totalSeconds % 60
  return `${mins}:${secs.toString().padStart(2, '0')}`
}

function ActiveHoldBanner({ hold }: { hold: StudentHold }) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])

  const diffSeconds = Math.max(0, Math.floor((new Date(hold.expiresAt).getTime() - now) / 1000))
  if (diffSeconds <= 0) return null

  return (
    <div className="rounded-xl border-2 border-amber-500/60 bg-gradient-to-r from-amber-500/15 via-amber-500/10 to-transparent p-5 sm:p-6 shadow-md">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-500/20 px-3 py-1 text-xs font-semibold text-amber-900 dark:text-amber-200">
            <span className="size-2 rounded-full bg-amber-500 animate-pulse" />
            আপনার একটি চলমান আবেদন রয়েছে (Ongoing Application)
          </span>
          <h3 className="mt-2 text-2xl font-bold tracking-tight text-foreground">{hold.className}</h3>
          <p className="mt-0.5 font-mono text-xs text-muted-foreground">Hold ID: {hold.holdId}</p>
        </div>

        <div className="flex flex-wrap items-center gap-4 sm:gap-6">
          <div className="text-left sm:text-right">
            <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              ফি প্রদানের বাকি সময়
            </p>
            <p className="mt-0.5 font-mono text-2xl font-extrabold text-amber-600 dark:text-amber-400">
              {formatCountdown(diffSeconds)}
            </p>
          </div>

          <Button asChild size="lg" className="h-11 px-6 font-bold shadow">
            <Link to={`/holds/${encodeURIComponent(hold.holdId)}`}>
              ভর্তি ফি দিন / বিস্তারিত →
            </Link>
          </Button>
        </div>
      </div>
    </div>
  )
}

export function ClassesPage() {
  const [classesList, setClassesList] = useState<ClassSummary[] | null>(null)
  const queryClient = useQueryClient()
  const { studentId } = useStudentId()

  const classesQuery = useQuery({
    queryKey: classesKey,
    queryFn: ({ signal }) => listClasses(signal),
  })

  const myHoldsQuery = useQuery({
    queryKey: ['my-holds', studentId],
    queryFn: ({ signal }) => getMyHolds(studentId, signal),
    enabled: Boolean(studentId),
    refetchInterval: 3000,
  })

  useClassesStream((updated) => {
    setClassesList(updated)
    queryClient.setQueryData(classesKey, updated)
  })

  const displayedClasses = classesList ?? classesQuery.data

  const activeHold = myHoldsQuery.data?.find(
    (h) => h.status === 'HELD' && new Date(h.expiresAt).getTime() > Date.now()
  )

  return (
    <section className="space-y-8">
      {/* Ongoing application banner if student has one */}
      {activeHold && <ActiveHoldBanner hold={activeHold} />}

      <div className="flex flex-wrap items-end justify-between gap-4 border-b pb-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
            অনলাইন ভর্তি কার্যক্রম
          </p>
          <h1 className="mt-1 text-3xl font-bold tracking-tight">শ্রেণি তালিকা ও খালি সিট (Class Catalogue)</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            প্রতিটি শিফটে সীমিত সংখ্যক সিট রয়েছে। "আগে আসলে আগে পাবেন" ভিত্তিতে ভর্তি নেওয়া হচ্ছে।
          </p>
        </div>
        <Button asChild variant="outline">
          <Link to="/admin">+ নতুন ক্লাস যোগ করুন (Admin)</Link>
        </Button>
      </div>

      {classesQuery.isPending && !displayedClasses && (
        <p role="status" className="rounded-xl border bg-card p-6 text-muted-foreground">
          ক্লাসের তালিকা লোড হচ্ছে…
        </p>
      )}

      {classesQuery.isError && !displayedClasses && (
        <div role="alert" className="rounded-xl border border-destructive/40 bg-card p-6">
          <p className="font-semibold">ক্লাসের তালিকা লোড করা যায়নি।</p>
          <p className="mt-1 text-sm text-muted-foreground">{classesQuery.error.message}</p>
          <Button className="mt-4" variant="outline" onClick={() => void classesQuery.refetch()}>
            আবার চেষ্টা করুন (Try again)
          </Button>
        </div>
      )}

      {displayedClasses && displayedClasses.length === 0 && (
        <div className="rounded-xl border border-dashed bg-card px-6 py-12 text-center">
          <h3 className="text-lg font-semibold">কোনো ক্লাস পাওয়া যায়নি</h3>
          <p className="mt-2 text-sm text-muted-foreground">প্রশাসন থেকে নতুন ক্লাস যুক্ত করুন।</p>
          <Button asChild className="mt-5">
            <Link to="/admin">নতুন ক্লাস তৈরি করুন</Link>
          </Button>
        </div>
      )}

      {displayedClasses && displayedClasses.length > 0 && (
        <ul className="grid gap-5 md:grid-cols-2">
          {displayedClasses.map((item) => (
            <li
              key={item.id}
              className="rounded-xl border bg-card p-5 sm:p-6 shadow-sm transition-all hover:border-foreground/30 hover:shadow-md"
            >
              <div className="flex items-start justify-between gap-4">
                <div>
                  <Link
                    to={`/classes/${encodeURIComponent(item.id)}`}
                    className="text-xl font-bold text-foreground hover:underline"
                  >
                    {item.name}
                  </Link>
                  <p className="mt-1 text-sm font-medium text-emerald-600 dark:text-emerald-400">
                    {item.availableSeats} টি সিট খালি আছে (Available)
                  </p>
                </div>
                <span className="rounded-full bg-secondary px-3 py-1 font-mono text-xs font-semibold">
                  খালি: {item.availableSeats}/{item.totalSeats}
                </span>
              </div>

              <dl className="mt-5 grid grid-cols-3 gap-2 rounded-lg bg-muted/40 p-3 text-center text-xs">
                <div>
                  <dt className="text-muted-foreground">মোট (Total)</dt>
                  <dd className="mt-1 font-mono text-base font-bold">{item.totalSeats}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">হোল্ড (Held)</dt>
                  <dd className="mt-1 font-mono text-base font-bold text-amber-600 dark:text-amber-400">{item.heldSeats}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">নিশ্চিত (Confirmed)</dt>
                  <dd className="mt-1 font-mono text-base font-bold text-blue-600 dark:text-blue-400">{item.confirmedSeats}</dd>
                </div>
              </dl>

              <div className="mt-5 flex items-center justify-between border-t pt-4">
                <Link
                  to={`/classes/${encodeURIComponent(item.id)}`}
                  className="text-xs font-semibold text-muted-foreground hover:text-foreground"
                >
                  বিস্তারিত দেখুন (Details) →
                </Link>

                <Button asChild size="sm" disabled={item.availableSeats <= 0} className="font-semibold">
                  <Link to={`/classes/${encodeURIComponent(item.id)}`}>
                    {item.availableSeats > 0 ? 'আবেদন করুন (Apply)' : 'সিট পূর্ণ (Full)'}
                  </Link>
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
