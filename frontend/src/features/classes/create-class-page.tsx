import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api'
import { classesKey, createClass, listClasses } from './classes-api'

export function CreateClassPage() {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [seats, setSeats] = useState('')
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [errors, setErrors] = useState<{ name?: string; seats?: string }>({})

  const classesQuery = useQuery({
    queryKey: classesKey,
    queryFn: ({ signal }) => listClasses(signal),
  })

  const create = useMutation({
    mutationFn: createClass,
    onSuccess: async (data) => {
      setSuccessMessage(`Class "${data.name}" was successfully created with ${data.totalSeats} seats!`)
      setName('')
      setSeats('')
      setErrors({})
      await queryClient.invalidateQueries({ queryKey: classesKey })
    },
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSuccessMessage(null)
    const trimmedName = name.trim()
    const capacity = Number(seats)
    const nextErrors: typeof errors = {}

    if (!trimmedName) {
      nextErrors.name = 'ক্লাসের নাম লিখুন (যেমন "Class 6 - Morning Shift")।'
    } else if ([...trimmedName].length > 200) {
      nextErrors.name = 'ক্লাসের নাম ২০০ অক্ষরের মধ্যে হতে হবে।'
    }

    if (!seats) {
      nextErrors.seats = 'মোট সিট সংখ্যা উল্লেখ করুন।'
    } else if (!Number.isSafeInteger(capacity) || capacity < 1 || capacity > 2147483647) {
      nextErrors.seats = 'একটি সঠিক ধনাত্মক পূর্ণসংখ্যা দিন (যেমন ১২০)।'
    }

    setErrors(nextErrors)
    create.reset()
    if (Object.keys(nextErrors).length === 0) {
      create.mutate({ name: trimmedName, totalSeats: capacity })
    }
  }

  return (
    <section className="mx-auto max-w-4xl space-y-10">
      <div className="flex items-center justify-between border-b pb-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">বিদ্যালয় প্রশাসন (SCHOOL ADMINISTRATION)</p>
          <h1 className="mt-1 text-3xl font-bold tracking-tight">ক্লাস তৈরি ও সিট ব্যবস্থাপনা</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            প্রতিটি শিফটকে আলাদা ক্লাস হিসেবে তৈরি করুন এবং সিট সংখ্যা নির্ধারণ করুন।
          </p>
        </div>
        <Button asChild variant="outline">
          <Link to="/classes">← সাধারণ শ্রেণি তালিকা দেখুন</Link>
        </Button>
      </div>

      {/* Class Creation Form Card */}
      <div className="rounded-xl border bg-card p-6 shadow-sm sm:p-8">
        <div className="mb-6">
          <h2 className="text-xl font-bold">নতুন ক্লাস তৈরি করুন (Create Class)</h2>
          <p className="text-xs text-muted-foreground">
            ক্লাসের নাম এবং মোট সিট সংখ্যা লিখে নিচের <strong>ক্লাস তৈরি করুন</strong> বাটনে চাপ দিন।
          </p>
        </div>

        {successMessage && (
          <div role="status" className="mb-6 rounded-lg border border-emerald-500/40 bg-emerald-500/10 p-4 text-sm text-emerald-800 dark:text-emerald-300">
            <p className="font-semibold">✓ {successMessage}</p>
          </div>
        )}

        {create.isError && (
          <div role="alert" className="mb-6 rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive dark:text-destructive-foreground">
            <p className="font-semibold">ক্লাস তৈরিতে সমস্যা হয়েছে</p>
            <p className="mt-1">
              {create.error instanceof ApiError && create.error.status === 409
                ? 'এই নামের একটি ক্লাস ইতিমধ্যে বিদ্যমান রয়েছে।'
                : create.error.message}
            </p>
          </div>
        )}

        <form onSubmit={submit} noValidate className="space-y-6">
          <div className="grid gap-6 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="class-name" className="text-sm font-semibold">
                ক্লাসের নাম (Class Name) <span className="text-destructive">*</span>
              </Label>
              <Input
                id="class-name"
                name="name"
                placeholder='যেমন: "Class 6 - Morning Shift"'
                value={name}
                maxLength={200}
                autoComplete="off"
                aria-invalid={Boolean(errors.name)}
                aria-describedby={errors.name ? 'class-name-error' : 'class-name-help'}
                onChange={(event) => setName(event.target.value)}
                className="h-10"
              />
              <p
                id={errors.name ? 'class-name-error' : 'class-name-help'}
                className={errors.name ? 'text-xs text-destructive' : 'text-xs text-muted-foreground'}
              >
                {errors.name ?? '১ থেকে ২০০ অক্ষরের মধ্যে নাম দিন। প্রতিটি শিফট আলাদা ক্লাস হিসেবে তৈরি হবে।'}
              </p>
              <div className="flex flex-wrap items-center gap-1.5 pt-1">
                <span className="text-[11px] text-muted-foreground">নমুনা শিফট:</span>
                {['Class 6 - Morning Shift', 'Class 6 - Day Shift', 'Class 7 - Morning Shift', 'Class 8 - Day Shift'].map((preset) => (
                  <button
                    key={preset}
                    type="button"
                    onClick={() => setName(preset)}
                    className="rounded-md border bg-muted/60 px-2 py-0.5 text-[11px] font-medium text-foreground hover:bg-muted transition-colors"
                  >
                    + {preset}
                  </button>
                ))}
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="total-seats" className="text-sm font-semibold">
                মোট সিট সংখ্যা (Total Seats) <span className="text-destructive">*</span>
              </Label>
              <Input
                id="total-seats"
                name="totalSeats"
                type="number"
                min="1"
                max="2147483647"
                step="1"
                placeholder="যেমন: ১২০"
                inputMode="numeric"
                value={seats}
                aria-invalid={Boolean(errors.seats)}
                aria-describedby={errors.seats ? 'total-seats-error' : undefined}
                onChange={(event) => setSeats(event.target.value)}
                className="h-10"
              />
              {errors.seats && <p id="total-seats-error" className="text-xs text-destructive">{errors.seats}</p>}
              <div className="flex flex-wrap items-center gap-1.5 pt-1">
                <span className="text-[11px] text-muted-foreground">সিট সংখ্যা:</span>
                {[40, 60, 100, 120].map((num) => (
                  <button
                    key={num}
                    type="button"
                    onClick={() => setSeats(String(num))}
                    className="rounded-md border bg-muted/60 px-2 py-0.5 text-[11px] font-medium text-foreground hover:bg-muted transition-colors"
                  >
                    {num} টি
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-4 pt-2">
            <Button
              type="submit"
              disabled={create.isPending}
              size="lg"
              className="h-11 px-8 text-sm font-bold shadow-md"
            >
              {create.isPending ? 'ক্লাস তৈরি হচ্ছে…' : '+ ক্লাস তৈরি করুন (Create Class)'}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setName('')
                setSeats('')
                setErrors({})
                setSuccessMessage(null)
              }}
              className="h-11"
            >
              রিসেট (Reset)
            </Button>
          </div>
        </form>
      </div>

      {/* Existing Classes Overview */}
      <div className="space-y-4">
        <h2 className="text-xl font-bold">সিস্টেমে বিদ্যমান ক্লাসসমূহ (Existing Classes)</h2>
        {classesQuery.isPending && (
          <p className="text-sm text-muted-foreground">ক্লাস লোড হচ্ছে…</p>
        )}
        {classesQuery.isSuccess && classesQuery.data.length === 0 && (
          <p className="rounded-xl border border-dashed p-6 text-center text-sm text-muted-foreground">
            এখনো কোনো ক্লাস তৈরি করা হয়নি। উপরের ফরমটি পূরণ করে প্রথম ক্লাসটি তৈরি করুন।
          </p>
        )}
        {classesQuery.isSuccess && classesQuery.data.length > 0 && (
          <div className="overflow-x-auto rounded-xl border bg-card shadow-sm">
            <table className="w-full text-left text-sm">
              <thead className="border-b bg-muted/50 text-xs font-semibold uppercase text-muted-foreground">
                <tr>
                  <th className="px-5 py-3">ক্লাসের নাম</th>
                  <th className="px-5 py-3 text-right">মোট সিট</th>
                  <th className="px-5 py-3 text-right">হোল্ডে আছে</th>
                  <th className="px-5 py-3 text-right">ভর্তি নিশ্চিত</th>
                  <th className="px-5 py-3 text-right">খালি সিট</th>
                  <th className="px-5 py-3 text-center">পদক্ষেপ</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {classesQuery.data.map((c) => (
                  <tr key={c.id} className="hover:bg-muted/30">
                    <td className="px-5 py-3.5 font-medium">{c.name}</td>
                    <td className="px-5 py-3.5 text-right font-mono">{c.totalSeats}</td>
                    <td className="px-5 py-3.5 text-right font-mono text-amber-600 dark:text-amber-400">{c.heldSeats}</td>
                    <td className="px-5 py-3.5 text-right font-mono text-emerald-600 dark:text-emerald-400">{c.confirmedSeats}</td>
                    <td className="px-5 py-3.5 text-right font-mono font-bold text-foreground">{c.availableSeats}</td>
                    <td className="px-5 py-3.5 text-center">
                      <Button asChild size="xs" variant="outline">
                        <Link to={`/classes/${encodeURIComponent(c.id)}`}>দেখুন</Link>
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  )
}
