import { useEffect, useRef, useState } from 'react'
import type { ClassSummary } from '@/features/classes/contracts'
import { listClasses } from '@/features/classes/classes-api'

const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''

export function useClassesStream(onUpdate: (classes: ClassSummary[]) => void): { isStreaming: boolean } {
  const [isStreaming, setIsStreaming] = useState(false)
  const onUpdateRef = useRef(onUpdate)

  useEffect(() => {
    onUpdateRef.current = onUpdate
  }, [onUpdate])

  useEffect(() => {
    let es: EventSource | null = null
    let pollInterval: ReturnType<typeof setInterval> | null = null
    let isDisposed = false

    const startPolling = () => {
      if (pollInterval || isDisposed) return
      pollInterval = setInterval(async () => {
        try {
          const classes = await listClasses()
          if (!isDisposed && classes) {
            onUpdateRef.current(classes)
          }
        } catch {
          // ignore polling errors
        }
      }, 3000)
    }

    const stopPolling = () => {
      if (pollInterval) {
        clearInterval(pollInterval)
        pollInterval = null
      }
    }

    try {
      const url = `${apiBase}/classes/stream`
      es = new EventSource(url)

      es.onopen = () => {
        if (isDisposed) return
        setIsStreaming(true)
        stopPolling()
      }

      es.onmessage = (event) => {
        if (isDisposed) return
        setIsStreaming(true)
        stopPolling()
        try {
          const data = JSON.parse(event.data) as ClassSummary[]
          if (Array.isArray(data)) {
            onUpdateRef.current(data)
          }
        } catch {
          // ignore parse error
        }
      }

      es.onerror = () => {
        if (isDisposed) return
        setIsStreaming(false)
        startPolling()
      }
    } catch {
      startPolling()
    }

    return () => {
      isDisposed = true
      stopPolling()
      if (es) {
        es.close()
      }
    }
  }, [])

  return { isStreaming }
}
