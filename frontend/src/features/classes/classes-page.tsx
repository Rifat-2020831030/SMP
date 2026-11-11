import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { classesKey, listClasses } from './classes-api'

export function ClassesPage() {
  const classes = useQuery({
    queryKey: classesKey,
    queryFn: ({ signal }) => listClasses(signal),
  })

  return (
    <section className="space-y-7">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-medium text-muted-foreground">SCHOOL ADMISSION</p>
          <h2 className="mt-2 text-3xl font-semibold tracking-tight">Class catalogue</h2>
          <p className="mt-2 max-w-xl text-muted-foreground">Browse class capacity and seat availability.</p>
        </div>
        <Button asChild><Link to="/admin">Create a class</Link></Button>
      </div>

      {classes.isPending && <p role="status" className="rounded-xl border bg-card p-6 text-muted-foreground">Loading classes…</p>}

      {classes.isError && (
        <div role="alert" className="rounded-xl border border-destructive/40 bg-card p-6">
          <p className="font-medium">Classes could not be loaded.</p>
          <p className="mt-1 text-sm text-muted-foreground">{classes.error.message}</p>
          <Button className="mt-4" variant="outline" onClick={() => void classes.refetch()}>Try again</Button>
        </div>
      )}

      {classes.isSuccess && classes.data.length === 0 && (
        <div className="rounded-xl border border-dashed bg-card px-6 py-12 text-center">
          <h3 className="text-lg font-medium">No classes yet</h3>
          <p className="mt-2 text-sm text-muted-foreground">Create the first class to start the catalogue.</p>
          <Button asChild className="mt-5"><Link to="/admin">Create a class</Link></Button>
        </div>
      )}

      {classes.isSuccess && classes.data.length > 0 && (
        <ul className="grid gap-4 md:grid-cols-2">
          {classes.data.map((item) => (
            <li key={item.id}>
              <Link to={`/classes/${encodeURIComponent(item.id)}`} className="group block rounded-xl border bg-card p-5 transition-colors hover:border-foreground/30 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h3 className="text-xl font-semibold group-hover:underline">{item.name}</h3>
                    <p className="mt-1 text-sm text-muted-foreground">{item.availableSeats} seats available</p>
                  </div>
                  <span className="rounded-full bg-secondary px-3 py-1 text-sm font-medium">{item.availableSeats}/{item.totalSeats}</span>
                </div>
                <dl className="mt-6 grid grid-cols-3 gap-3 border-t pt-4 text-sm">
                  <div><dt className="text-muted-foreground">Total</dt><dd className="mt-1 font-semibold">{item.totalSeats}</dd></div>
                  <div><dt className="text-muted-foreground">Held</dt><dd className="mt-1 font-semibold">{item.heldSeats}</dd></div>
                  <div><dt className="text-muted-foreground">Confirmed</dt><dd className="mt-1 font-semibold">{item.confirmedSeats}</dd></div>
                </dl>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
