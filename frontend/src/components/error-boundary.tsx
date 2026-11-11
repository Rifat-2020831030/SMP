import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'

type State = { failed: boolean }

export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Application screen failed to render.', error, info.componentStack)
  }

  render() {
    if (this.state.failed) {
      return (
        <div role="alert" className="mx-auto max-w-xl rounded-xl border bg-card p-8 text-center">
          <h2 className="text-xl font-semibold">This page ran into a problem</h2>
          <p className="mt-2 text-muted-foreground">Your saved class information is still safe. Reload the page to try again.</p>
          <Button className="mt-5" onClick={() => window.location.reload()}>Reload page</Button>
        </div>
      )
    }
    return this.props.children
  }
}
