import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useStudentId } from '@/lib/student-id'
import { createHold } from '@/features/holds/holds-api'
import { getClass } from './classes-api'

export function ClassDetailsPage() {
  const { classId = '' } = useParams()
  const navigate = useNavigate()
  const { studentId, setStudentId } = useStudentId()
  const [inputStudentId, setInputStudentId] = useState(studentId || '')
  const [validationError, setValidationError] = useState<string | null>(null)

  const item = useQuery({
    queryKey: ['class', classId],
    queryFn: ({ signal }) => getClass(classId, signal),
    enabled: Boolean(classId),
    refetchInterval: 5000,
  })

  const applyMutation = useMutation({
    mutationFn: (targetStudentId: string) => createHold(classId, targetStudentId),
    onSuccess: (data) => {
      navigate(`/holds/${data.holdId}`)
    },
  })

  function handleApply(e: React.FormEvent) {
    e.preventDefault()
    setValidationError(null)
    const cleaned = inputStudentId.replace(/\D/g, '')
    if (cleaned.length !== 17) {
      setValidationError('সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন (Must be exactly 17 digits)')
      return
    }

    setStudentId(cleaned)
    applyMutation.mutate(cleaned)
  }

  return (
    <section className="mx-auto max-w-2xl space-y-6">
      <Link to="/classes" className="text-sm font-medium text-muted-foreground hover:text-foreground">
        ← সকল ক্লাসের তালিকা (All classes)
      </Link>

      {item.isPending && (
        <p role="status" className="rounded-xl border bg-card p-6 text-muted-foreground">
          ক্লাসের তথ্য লোড হচ্ছে…
        </p>
      )}

      {item.isError && (
        <div role="alert" className="rounded-xl border border-destructive/40 bg-card p-6">
          <h2 className="text-xl font-semibold">ক্লাসটি পাওয়া যায়নি (Class unavailable)</h2>
          <p className="mt-2 text-sm text-muted-foreground">{item.error.message}</p>
          {item.error instanceof Error && 'status' in item.error && item.error.status === 404 ? (
            <Button asChild className="mt-4" variant="outline">
              <Link to="/classes">ক্লাস তালিকায় ফিরে যান</Link>
            </Button>
          ) : (
            <Button className="mt-4" variant="outline" onClick={() => void item.refetch()}>
              আবার চেষ্টা করুন (Try again)
            </Button>
          )}
        </div>
      )}

      {item.isSuccess && (
        <article className="rounded-xl border bg-card p-6 sm:p-8 shadow-sm">
          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
            শ্রেণি বিবরণী (CLASS DETAILS)
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight">{item.data.name}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            সিট সংখ্যা ও বর্তমান বরাদ্দের বিবরণী
          </p>

          <dl className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-lg bg-muted/60 p-4">
              <dt className="text-xs font-medium text-muted-foreground">মোট সিট (Total)</dt>
              <dd className="mt-1 text-2xl font-bold">{item.data.totalSeats}</dd>
            </div>
            <div className="rounded-lg bg-emerald-500/10 p-4 text-emerald-900 dark:text-emerald-300">
              <dt className="text-xs font-medium">খালি সিট (Available)</dt>
              <dd className="mt-1 text-2xl font-bold">{item.data.availableSeats}</dd>
            </div>
            <div className="rounded-lg bg-amber-500/10 p-4 text-amber-900 dark:text-amber-300">
              <dt className="text-xs font-medium">হোল্ডে আছে (Held)</dt>
              <dd className="mt-1 text-2xl font-bold">{item.data.heldSeats}</dd>
            </div>
            <div className="rounded-lg bg-blue-500/10 p-4 text-blue-900 dark:text-blue-300">
              <dt className="text-xs font-medium">ভর্তি নিশ্চিত (Confirmed)</dt>
              <dd className="mt-1 text-2xl font-bold">{item.data.confirmedSeats}</dd>
            </div>
          </dl>

          <div className="mt-8 border-t pt-6">
            <div className="mb-4">
              <h3 className="text-lg font-semibold">ভর্তির আবেদন করুন (Apply for Seat)</h3>
              <p className="text-xs text-muted-foreground">
                আবেদন করলে সাময়িকভাবে সিটটি আপনার নামে সংরক্ষিত (Held) থাকবে। নির্দিষ্ট সময়ের মধ্যে ফি জমা দিলে ভর্তি নিশ্চিত হবে।
              </p>
            </div>

            {applyMutation.isError && (
              <div
                role="alert"
                className="mb-4 rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive dark:text-destructive-foreground"
              >
                <p className="font-semibold">আবেদন ব্যর্থ হয়েছে (Application failed)</p>
                <p className="mt-1">
                  {applyMutation.error instanceof Error
                    ? applyMutation.error.message
                    : 'একটি অপ্রত্যাশিত ত্রুটি ঘটেছে। আবার চেষ্টা করুন।'}
                </p>
              </div>
            )}

            {validationError && (
              <div
                role="alert"
                className="mb-4 rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive dark:text-destructive-foreground"
              >
                <p className="font-semibold">{validationError}</p>
              </div>
            )}

            {item.data.availableSeats <= 0 ? (
              <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-sm text-amber-900 dark:text-amber-300">
                <p className="font-semibold">সিট পূর্ণ (No Available Seats)</p>
                <p className="mt-1 text-xs">এই মুহূর্তে কোনো সিট খালি নেই। হোল্ড করা সিটের মেয়াদ শেষ হলে আবার চেষ্টা করুন।</p>
              </div>
            ) : (
              <form onSubmit={handleApply} className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="birth-reg-no" className="text-sm font-semibold">
                    শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (Birth Registration No.) <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="birth-reg-no"
                    type="text"
                    inputMode="numeric"
                    maxLength={17}
                    pattern="[0-9]{17}"
                    placeholder="১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন (17 digits)"
                    value={inputStudentId}
                    onChange={(e) => {
                      const cleaned = e.target.value.replace(/\D/g, '').slice(0, 17)
                      setInputStudentId(cleaned)
                      setValidationError(null)
                    }}
                    className="h-10 font-mono text-sm max-w-md"
                    required
                  />
                  <p className="text-xs text-muted-foreground">
                    {inputStudentId.length}/১৭ অঙ্ক প্রবেশ করানো হয়েছে।
                  </p>
                </div>

                <Button
                  type="submit"
                  disabled={applyMutation.isPending || item.data.availableSeats <= 0}
                  size="lg"
                  className="h-11 px-6 font-semibold"
                >
                  {applyMutation.isPending ? 'আবেদন প্রক্রিয়াধীন…' : 'আবেদন করুন (Apply for Seat)'}
                </Button>
              </form>
            )}
          </div>
        </article>
      )}
    </section>
  )
}
