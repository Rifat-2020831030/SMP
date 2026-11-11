import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useStudentId } from '@/lib/student-id'
import { getClass } from '@/features/classes/classes-api'
import { cancelHold, confirmHold, getHold, getMyHolds } from './holds-api'
import type { HoldStatus } from './contracts'

function formatCountdown(totalSeconds: number): string {
  const mins = Math.floor(totalSeconds / 60)
  const secs = totalSeconds % 60
  return `${mins}:${secs.toString().padStart(2, '0')}`
}

function StatusBadge({ status }: { status: HoldStatus['status'] }) {
  const meta: Record<HoldStatus['status'], { label: string; style: string }> = {
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
    <span className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-bold uppercase tracking-wider ${style}`}>
      {label}
    </span>
  )
}

export function HoldPage() {
  const { holdId = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { studentId, setStudentId } = useStudentId()
  const [inputStudentId, setInputStudentId] = useState('')

  const hold = useQuery({
    queryKey: ['hold', holdId],
    queryFn: ({ signal }) => getHold(holdId, studentId, signal),
    enabled: Boolean(holdId && studentId),
    refetchInterval: 3000,
  })

  const classQuery = useQuery({
    queryKey: ['class', hold.data?.classId],
    queryFn: ({ signal }) => getClass(hold.data!.classId, signal),
    enabled: Boolean(hold.data?.classId),
  })

  const myHolds = useQuery({
    queryKey: ['my-holds', studentId],
    queryFn: ({ signal }) => getMyHolds(studentId, signal),
    enabled: Boolean(studentId),
  })

  const studentHold = myHolds.data?.find((h) => h.holdId === holdId)
  const className = studentHold?.className || classQuery.data?.name || hold.data?.classId || 'শ্রেণি'

  // Countdown timer
  const isHeld = hold.data?.status === 'HELD'
  const expiresAt = hold.data?.status === 'HELD' ? hold.data.expiresAt : null
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!expiresAt) return

    const timer = setInterval(() => {
      setNow(Date.now())
    }, 1000)
    return () => clearInterval(timer)
  }, [expiresAt])

  const secondsLeft = expiresAt
    ? Math.max(0, Math.floor((new Date(expiresAt).getTime() - now) / 1000))
    : null

  const isExpired =
    hold.data?.status === 'EXPIRED' ||
    (isHeld && secondsLeft !== null && secondsLeft <= 0)

  const effectiveStatus: HoldStatus['status'] = isExpired
    ? 'EXPIRED'
    : (hold.data?.status ?? 'HELD')

  const confirmMutation = useMutation({
    mutationFn: () => confirmHold(holdId, studentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hold', holdId] })
      queryClient.invalidateQueries({ queryKey: ['my-holds', studentId] })
      queryClient.invalidateQueries({ queryKey: ['classes'] })
    },
  })

  const cancelMutation = useMutation({
    mutationFn: () => cancelHold(holdId, studentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-holds', studentId] })
      queryClient.invalidateQueries({ queryKey: ['classes'] })
      navigate('/me/holds')
    },
  })

  if (!studentId) {
    return (
      <section className="mx-auto max-w-2xl space-y-6">
        <Link to="/classes" className="text-sm font-medium text-muted-foreground hover:text-foreground">
          ← সকল ক্লাস (All classes)
        </Link>
        <div className="rounded-xl border bg-card p-6 sm:p-8 shadow-sm">
          <h2 className="text-xl font-bold">শিক্ষার্থীর পরিচয় প্রদান করুন (Student Identification)</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            এই আবেদনের তথ্য দেখতে এবং ভর্তি ফি জমা দিতে আপনার ১৭ অঙ্কের জন্ম নিবন্ধন নম্বরটি প্রদান করুন।
          </p>

          <form
            onSubmit={(e) => {
              e.preventDefault()
              const cleaned = inputStudentId.replace(/\D/g, '')
              if (cleaned.length === 17) {
                setStudentId(cleaned)
              }
            }}
            className="mt-6 flex flex-wrap items-center gap-3"
          >
            <div className="flex-1 min-w-[240px]">
              <Label htmlFor="hold-student-id" className="sr-only">জন্ম নিবন্ধন নম্বর</Label>
              <Input
                id="hold-student-id"
                type="text"
                inputMode="numeric"
                maxLength={17}
                pattern="[0-9]{17}"
                placeholder="১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন"
                value={inputStudentId}
                onChange={(e) => setInputStudentId(e.target.value.replace(/\D/g, '').slice(0, 17))}
                className="h-10 font-mono text-sm"
                required
              />
            </div>
            <Button type="submit" disabled={inputStudentId.replace(/\D/g, '').length !== 17} className="h-10 px-5">
              আবেদন দেখুন (View Hold)
            </Button>
          </form>
        </div>
      </section>
    )
  }

  return (
    <section className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center justify-between">
        <Link to="/classes" className="text-sm font-medium text-muted-foreground hover:text-foreground">
          ← সকল ক্লাসের তালিকা
        </Link>
        <Link to="/me/holds" className="text-sm font-medium text-muted-foreground hover:text-foreground">
          আমার আবেদনসমূহ →
        </Link>
      </div>

      {hold.isPending && (
        <p role="status" className="rounded-xl border bg-card p-6 text-muted-foreground">
          আবেদনের তথ্য লোড হচ্ছে…
        </p>
      )}

      {hold.isError && (
        <div role="alert" className="rounded-xl border border-destructive/40 bg-card p-6">
          <h2 className="text-xl font-bold">আবেদন পাওয়া যায়নি (Hold unavailable)</h2>
          <p className="mt-2 text-sm text-muted-foreground">{hold.error.message}</p>
          {hold.error instanceof Error && 'status' in hold.error && hold.error.status === 404 ? (
            <div className="mt-4 flex gap-3">
              <Button asChild variant="outline">
                <Link to="/classes">ক্লাস তালিকা দেখুন</Link>
              </Button>
              <Button asChild variant="outline">
                <Link to="/me/holds">আমার আবেদনসমূহ</Link>
              </Button>
            </div>
          ) : (
            <Button className="mt-4" variant="outline" onClick={() => void hold.refetch()}>
              আবার চেষ্টা করুন (Try again)
            </Button>
          )}
        </div>
      )}

      {hold.isSuccess && (
        <article className="rounded-xl border bg-card p-6 sm:p-8 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-4 border-b pb-5">
            <div>
              <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                ভর্তির আবেদন (APPLICATION HOLD)
              </p>
              <h2 className="mt-1 text-3xl font-bold tracking-tight">{className}</h2>
              <div className="mt-1 flex items-center gap-2">
                <span className="font-mono text-xs text-muted-foreground">হোল্ড আইডি: {hold.data.holdId}</span>
                <button
                  type="button"
                  onClick={() => {
                    void navigator.clipboard.writeText(hold.data.holdId)
                    alert('হোল্ড আইডি কপি করা হয়েছে!')
                  }}
                  className="rounded border px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground hover:bg-muted"
                >
                  কপি
                </button>
              </div>
            </div>
            <StatusBadge status={effectiveStatus} />
          </div>

          {hold.data.status === 'HELD' && secondsLeft !== null && secondsLeft <= 120 && secondsLeft > 0 && (
            <div className="mt-5 rounded-lg border border-destructive/60 bg-destructive/15 p-4 text-sm text-destructive dark:text-red-200 animate-pulse">
              <p className="font-bold flex items-center gap-1.5">
                <span>⚠️ সতর্কতা:</span> ফি প্রদানের সময় শেষ হতে ২ মিনিটেরও কম বাকি রয়েছে!
              </p>
              <p className="mt-0.5 text-xs">
                মেয়াদ শেষ হলে সিটটি স্বয়ংক্রিয়ভাবে বাতিল হয়ে যাবে। অবিলম্বে নিচের "ভর্তি ফি দিন" বাটনে চাপ দিন।
              </p>
            </div>
          )}

          <div className="mt-6 rounded-xl bg-muted/50 p-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  মেয়াদ শেষের সময় (Expires At)
                </p>
                <p className="mt-1 text-sm font-medium">
                  {new Date(hold.data.expiresAt).toLocaleTimeString('bn-BD', {
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit',
                  })}
                  {' · '}
                  {new Date(hold.data.expiresAt).toLocaleDateString('bn-BD')}
                </p>
              </div>

              {hold.data.status === 'HELD' && (
                <div className="text-left sm:text-right">
                  <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                    মেয়াদ শেষ হতে বাকি (Time Remaining)
                  </p>
                  <p
                    className={`mt-1 font-mono text-2xl font-extrabold ${
                      isExpired ? 'text-destructive' : 'text-amber-600 dark:text-amber-400'
                    }`}
                  >
                    {isExpired ? 'মেয়াদ শেষ (EXPIRED)' : formatCountdown(secondsLeft ?? 0)}
                  </p>
                </div>
              )}
            </div>
          </div>

          {confirmMutation.isError && (
            <div
              role="alert"
              className="mt-5 rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive dark:text-destructive-foreground"
            >
              <p className="font-semibold">ভর্তি ফি প্রদান ব্যর্থ হয়েছে</p>
              <p className="mt-1">
                {confirmMutation.error instanceof Error
                  ? confirmMutation.error.message
                  : 'ফি জমা দেওয়া সম্ভব হয়নি। আবার চেষ্টা করুন।'}
              </p>
            </div>
          )}

          {cancelMutation.isError && (
            <div
              role="alert"
              className="mt-5 rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive dark:text-destructive-foreground"
            >
              <p className="font-semibold">আবেদন বাতিল ব্যর্থ হয়েছে</p>
              <p className="mt-1">
                {cancelMutation.error instanceof Error
                  ? cancelMutation.error.message
                  : 'আবেদন বাতিল করা সম্ভব হয়নি।'}
              </p>
            </div>
          )}

          {effectiveStatus === 'CONFIRMED' && (
            <div className="mt-6 rounded-xl border border-emerald-500/40 bg-emerald-500/10 p-5 text-sm text-emerald-900 dark:text-emerald-200">
              <p className="text-lg font-bold">🎉 ভর্তি নিশ্চিত হয়েছে! (Admission Confirmed)</p>
              <p className="mt-1">
                আপনার ভর্তি ফি সফলভাবে গৃহীত হয়েছে এবং সিটটি স্থায়ীভাবে নিশ্চিত করা হয়েছে।
              </p>
            </div>
          )}

          {effectiveStatus === 'EXPIRED' && (
            <div className="mt-6 rounded-xl border border-muted bg-muted/40 p-5 text-sm text-muted-foreground">
              <p className="font-bold text-foreground">আবেদনের মেয়াদ শেষ হয়েছে (Hold Expired)</p>
              <p className="mt-1">
                নির্ধারিত সময়ের মধ্যে ভর্তি ফি জমা না দেওয়ায় সিটটি আবার অন্যদের জন্য উন্মুক্ত করে দেওয়া হয়েছে। সিট খালি থাকলে পুনরায় আবেদন করতে পারেন।
              </p>
              <Button asChild className="mt-4" variant="outline">
                <Link to="/classes">ক্লাস তালিকায় ফিরে যান</Link>
              </Button>
            </div>
          )}

          {effectiveStatus === 'CANCELLED' && (
            <div className="mt-6 rounded-xl border border-muted bg-muted/40 p-5 text-sm text-muted-foreground">
              <p className="font-bold text-foreground">আবেদন বাতিল করা হয়েছে (Hold Cancelled)</p>
              <p className="mt-1">
                এই আবেদনের সিটটি মুক্ত করে দেওয়া হয়েছে।
              </p>
              <Button asChild className="mt-4" variant="outline">
                <Link to="/classes">ক্লাস তালিকায় ফিরে যান</Link>
              </Button>
            </div>
          )}

          {hold.data.status === 'HELD' && (
            <div className="mt-8 flex flex-wrap items-center gap-4 border-t pt-6">
              {!isExpired && (
                <Button
                  onClick={() => confirmMutation.mutate()}
                  disabled={confirmMutation.isPending || cancelMutation.isPending || isExpired}
                  size="lg"
                  className="h-11 px-8 font-bold bg-emerald-600 hover:bg-emerald-700 text-white shadow-md"
                >
                  {confirmMutation.isPending ? 'প্রক্রিয়াধীন…' : 'ভর্তি ফি দিন (Pay Fee)'}
                </Button>
              )}
              <Button
                variant="outline"
                size="lg"
                onClick={() => cancelMutation.mutate()}
                disabled={confirmMutation.isPending || cancelMutation.isPending}
                className="h-11 px-6 font-semibold text-destructive hover:bg-destructive/10"
              >
                {cancelMutation.isPending ? 'বাতিল হচ্ছে…' : 'আবেদন বাতিল (Cancel)'}
              </Button>
            </div>
          )}
        </article>
      )}
    </section>
  )
}
