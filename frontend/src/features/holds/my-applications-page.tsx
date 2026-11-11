import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useStudentId } from '@/lib/student-id'
import { getMyHolds } from './holds-api'
import type { StudentHold } from './contracts'

function formatCountdown(totalSeconds: number): string {
  const mins = Math.floor(totalSeconds / 60)
  const secs = totalSeconds % 60
  return `${mins}:${secs.toString().padStart(2, '0')}`
}

function StatusBadge({ status }: { status: StudentHold['status'] }) {
  const meta: Record<StudentHold['status'], { label: string; style: string }> = {
    HELD: {
      label: 'হোল্ডে আছে (HELD)',
      style: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-300 border-amber-300 dark:border-amber-800',
    },
    CONFIRMED: {
      label: 'ভর্তি নিশ্চিত (CONFIRMED)',
      style: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-300 border-emerald-300 dark:border-emerald-800',
    },
    EXPIRED: {
      label: 'মেয়াদোত্তীর্ণ (EXPIRED)',
      style: 'bg-muted text-muted-foreground border-border',
    },
    CANCELLED: {
      label: 'বাতিলকৃত (CANCELLED)',
      style: 'bg-muted text-muted-foreground border-border',
    },
  }

  const { label, style } = meta[status]

  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold uppercase tracking-wider ${style}`}>
      {label}
    </span>
  )
}

function HoldRow({ hold }: { hold: StudentHold }) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (hold.status !== 'HELD') return
    const interval = setInterval(() => {
      setNow(Date.now())
    }, 1000)
    return () => clearInterval(interval)
  }, [hold.status])

  const diffSeconds = Math.max(0, Math.floor((new Date(hold.expiresAt).getTime() - now) / 1000))
  const isExpired = hold.status === 'EXPIRED' || (hold.status === 'HELD' && diffSeconds <= 0)
  const effectiveStatus = isExpired ? 'EXPIRED' : hold.status

  return (
    <li key={hold.holdId}>
      <Link
        to={`/holds/${encodeURIComponent(hold.holdId)}`}
        className="group block rounded-xl border bg-card p-5 sm:p-6 transition-all hover:border-foreground/30 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="text-xl font-bold group-hover:underline">{hold.className}</h3>
            <p className="mt-0.5 font-mono text-xs text-muted-foreground">হোল্ড আইডি: {hold.holdId}</p>
          </div>
          <div className="flex items-center gap-3">
            {hold.status === 'HELD' && !isExpired && (
              <span className="font-mono text-sm font-bold text-amber-600 dark:text-amber-400">
                বাকি: {formatCountdown(diffSeconds)}
              </span>
            )}
            <StatusBadge status={effectiveStatus} />
          </div>
        </div>

        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t pt-3 text-xs text-muted-foreground">
          <span>
            মেয়াদ শেষের সময়:{' '}
            {new Date(hold.expiresAt).toLocaleTimeString('bn-BD', { hour: '2-digit', minute: '2-digit' })}{' · '}
            {new Date(hold.expiresAt).toLocaleDateString('bn-BD')}
          </span>
          <span className="font-bold text-foreground group-hover:underline">আবেদনের বিস্তারিত ও ফি প্রদান →</span>
        </div>
      </Link>
    </li>
  )
}

export function MyApplicationsPage() {
  const { studentId, setStudentId } = useStudentId()
  const [inputStudentId, setInputStudentId] = useState(studentId || '')
  const [validationError, setValidationError] = useState<string | null>(null)

  const holds = useQuery({
    queryKey: ['my-holds', studentId],
    queryFn: ({ signal }) => getMyHolds(studentId, signal),
    enabled: Boolean(studentId),
    refetchInterval: 5000,
  })

  function handleLookup(e: React.FormEvent) {
    e.preventDefault()
    setValidationError(null)
    const cleaned = inputStudentId.replace(/\D/g, '')
    if (cleaned.length !== 17) {
      setValidationError('সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন (17 digits required)')
      return
    }
    setStudentId(cleaned)
  }

  return (
    <section className="space-y-8">
      <div className="flex flex-wrap items-end justify-between gap-4 border-b pb-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">শিক্ষার্থীর ড্যাশবোর্ড</p>
          <h1 className="mt-1 text-3xl font-bold tracking-tight">আমার আবেদনসমূহ (My Applications)</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            আপনার সকল সক্রিয় ও পূর্ববর্তী ভর্তির আবেদন এবং ফি জমা দেওয়ার বর্তমান অবস্থা।
          </p>
        </div>
        <Button asChild variant="outline">
          <Link to="/classes">← সকল ক্লাস দেখুন</Link>
        </Button>
      </div>

      {/* Student ID Lookup or Switcher */}
      <div className="rounded-xl border bg-card p-5 sm:p-6 shadow-sm">
        <form onSubmit={handleLookup} className="flex flex-wrap items-end gap-3">
          <div className="flex-1 min-w-[260px] space-y-1.5">
            <Label htmlFor="my-holds-student-id" className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
              জন্ম নিবন্ধন নম্বর (17-Digit Birth Registration No.)
            </Label>
            <Input
              id="my-holds-student-id"
              type="text"
              inputMode="numeric"
              maxLength={17}
              pattern="[0-9]{17}"
              placeholder="১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন"
              value={inputStudentId}
              onChange={(e) => {
                const cleaned = e.target.value.replace(/\D/g, '').slice(0, 17)
                setInputStudentId(cleaned)
                setValidationError(null)
              }}
              className="h-10 font-mono text-sm"
              required
            />
          </div>
          <Button type="submit" className="h-10 px-5 font-semibold">
            {studentId ? 'আইডি পরিবর্তন / খুঁজুন' : 'আবেদন দেখুন (View Applications)'}
          </Button>
        </form>

        {validationError && (
          <p className="mt-2 text-xs text-destructive">{validationError}</p>
        )}

        {studentId && (
          <p className="mt-3 text-xs text-muted-foreground">
            বর্তমান শিক্ষার্থী আইডি:{' '}
            <span className="font-mono font-bold text-foreground">{studentId}</span>
          </p>
        )}
      </div>

      {!studentId && (
        <div className="rounded-xl border border-dashed bg-card p-8 text-center">
          <h3 className="text-lg font-semibold">আপনার জন্ম নিবন্ধন নম্বর দিন</h3>
          <p className="mt-1 text-sm text-muted-foreground">
            উপরে আপনার ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর প্রবেশ করিয়ে আবেদনসমূহ দেখুন।
          </p>
        </div>
      )}

      {studentId && (
        <>
          {holds.isPending && (
            <p role="status" className="rounded-xl border bg-card p-6 text-muted-foreground">
              আপনার আবেদনের তথ্য লোড হচ্ছে…
            </p>
          )}

          {holds.isError && (
            <div role="alert" className="rounded-xl border border-destructive/40 bg-card p-6">
              <p className="font-semibold">আবেদনসমূহ লোড করা সম্ভব হয়নি।</p>
              <p className="mt-1 text-sm text-muted-foreground">{holds.error.message}</p>
              <Button className="mt-4" variant="outline" onClick={() => void holds.refetch()}>
                আবার চেষ্টা করুন
              </Button>
            </div>
          )}

          {holds.isSuccess && holds.data.length === 0 && (
            <div className="rounded-xl border border-dashed bg-card px-6 py-12 text-center">
              <h3 className="text-lg font-semibold">কোনো আবেদন পাওয়া যায়নি</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                এই জন্ম নিবন্ধন নম্বরে এখনো কোনো ক্লাসে আবেদন করা হয়নি।
              </p>
              <Button asChild className="mt-5">
                <Link to="/classes">ক্লাস তালিকায় যান ও আবেদন করুন</Link>
              </Button>
            </div>
          )}

          {holds.isSuccess && holds.data.length > 0 && (
            <ul className="space-y-4">
              {holds.data.map((item) => (
                <HoldRow key={item.holdId} hold={item} />
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}
