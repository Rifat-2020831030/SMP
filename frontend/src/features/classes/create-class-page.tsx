import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api'
import { classesKey, createClass } from './classes-api'

export function CreateClassPage() {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [seats, setSeats] = useState('')
  const [errors, setErrors] = useState<{ name?: string; seats?: string }>({})
  const create = useMutation({
    mutationFn: createClass,
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: classesKey }),
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedName = name.trim()
    const capacity = Number(seats)
    const nextErrors: typeof errors = {}
    if (!trimmedName) nextErrors.name = 'Enter a class name.'
    else if ([...trimmedName].length > 200) nextErrors.name = 'Use 200 characters or fewer.'
    if (!seats) nextErrors.seats = 'Enter the total number of seats.'
    else if (!Number.isSafeInteger(capacity) || capacity < 1 || capacity > 2147483647) nextErrors.seats = 'Enter a positive whole number of seats.'
    setErrors(nextErrors)
    create.reset()
    if (Object.keys(nextErrors).length === 0) create.mutate({ name: trimmedName, totalSeats: capacity })
  }

  return (
    <section className="mx-auto max-w-2xl space-y-6">
      <Link to="/classes" className="text-sm font-medium text-muted-foreground hover:text-foreground">← All classes</Link>
      <div>
        <p className="text-sm font-medium text-muted-foreground">SCHOOL ADMIN</p>
        <h2 className="mt-2 text-3xl font-semibold tracking-tight">Create a class</h2>
        <p className="mt-2 text-muted-foreground">Set a class name and total seat capacity.</p>
      </div>

      {create.data ? (
        <div role="status" className="rounded-xl border bg-card p-6">
          <p className="font-semibold">{create.data.name} was created.</p>
          <p className="mt-1 text-sm text-muted-foreground">The class is now saved in the catalogue.</p>
          <div className="mt-5 flex flex-wrap gap-3">
            <Button asChild><Link to={`/classes/${encodeURIComponent(create.data.id)}`}>View class</Link></Button>
            <Button variant="outline" onClick={() => { create.reset(); setName(''); setSeats('') }}>Create another</Button>
          </div>
        </div>
      ) : (
        <form onSubmit={submit} noValidate className="space-y-5 rounded-xl border bg-card p-6 sm:p-8">
          <div className="space-y-2">
            <Label htmlFor="class-name">Class name</Label>
            <Input id="class-name" name="name" value={name} maxLength={202} autoComplete="off" aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? 'class-name-error' : 'class-name-help'} onChange={(event) => setName(event.target.value)} />
            <p id={errors.name ? 'class-name-error' : 'class-name-help'} className={errors.name ? 'text-sm text-destructive' : 'text-sm text-muted-foreground'}>{errors.name ?? 'Use 1 to 200 characters. Outer spaces are removed.'}</p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="total-seats">Total seats</Label>
            <Input id="total-seats" name="totalSeats" type="number" min="1" max="2147483647" step="1" inputMode="numeric" value={seats} aria-invalid={Boolean(errors.seats)} aria-describedby={errors.seats ? 'total-seats-error' : undefined} onChange={(event) => setSeats(event.target.value)} />
            {errors.seats && <p id="total-seats-error" className="text-sm text-destructive">{errors.seats}</p>}
          </div>
          {create.isError && (
            <div role="alert" className="rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm">
              <p className="font-medium">The class could not be created.</p>
              <p className="mt-1">{create.error instanceof ApiError && create.error.status === 409 ? 'A class with this name already exists.' : create.error.message}</p>
            </div>
          )}
          <Button type="submit" disabled={create.isPending} className="w-full sm:w-auto">
            {create.isPending ? 'Creating…' : 'Create class'}
          </Button>
        </form>
      )}
    </section>
  )
}
