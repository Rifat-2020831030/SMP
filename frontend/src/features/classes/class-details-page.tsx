import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { getClass } from './classes-api'

export function ClassDetailsPage() {
  const { classId = '' } = useParams()
  const item = useQuery({
    queryKey: ['class', classId],
    queryFn: ({ signal }) => getClass(classId, signal),
    enabled: Boolean(classId),
  })

  return (
    <section className="mx-auto max-w-2xl space-y-6">
      <Link to="/classes" className="text-sm font-medium text-muted-foreground hover:text-foreground">← All classes</Link>
      {item.isPending && <p role="status" className="rounded-xl border bg-card p-6 text-muted-foreground">Loading class…</p>}
      {item.isError && (
        <div role="alert" className="rounded-xl border border-destructive/40 bg-card p-6">
          <h2 className="text-xl font-semibold">Class unavailable</h2>
          <p className="mt-2 text-sm text-muted-foreground">{item.error.message}</p>
          {item.error instanceof Error && 'status' in item.error && item.error.status === 404
            ? <Button asChild className="mt-4" variant="outline"><Link to="/classes">Return to classes</Link></Button>
            : <Button className="mt-4" variant="outline" onClick={() => void item.refetch()}>Try again</Button>}
        </div>
      )}
      {item.isSuccess && (
        <article className="rounded-xl border bg-card p-6 sm:p-8">
          <p className="text-sm font-medium text-muted-foreground">CLASS DETAILS</p>
          <h2 className="mt-2 text-3xl font-semibold tracking-tight">{item.data.name}</h2>
          <p className="mt-2 text-muted-foreground">Seat capacity and current allocation.</p>
          <dl className="mt-8 grid gap-3 sm:grid-cols-2">
            {[
              ['Total seats', item.data.totalSeats],
              ['Available seats', item.data.availableSeats],
              ['Held seats', item.data.heldSeats],
              ['Confirmed seats', item.data.confirmedSeats],
            ].map(([label, count]) => (
              <div key={label} className="rounded-lg bg-muted/60 p-4">
                <dt className="text-sm text-muted-foreground">{label}</dt>
                <dd className="mt-1 text-2xl font-semibold">{count}</dd>
              </div>
            ))}
          </dl>
        </article>
      )}
    </section>
  )
}
