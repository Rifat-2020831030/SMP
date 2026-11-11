export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${apiBase}${path}`, options)
  } catch {
    throw new ApiError('The server could not be reached. Check your connection and try again.', 0)
  }

  if (response.status === 204) return undefined as T
  const body = await response.text()
  if (!response.ok) {
    let message = `The request failed (${response.status}). Please try again.`
    if (body) {
      try {
        const problem = JSON.parse(body) as { detail?: string; title?: string }
        message = problem.detail || problem.title || message
      } catch {
        message = 'The server returned an unreadable response. Please try again.'
      }
    }
    throw new ApiError(message, response.status)
  }

  if (!body) return undefined as T
  try {
    return JSON.parse(body) as T
  } catch {
    throw new ApiError('The server returned an unreadable response. Please try again.', response.status)
  }
}
