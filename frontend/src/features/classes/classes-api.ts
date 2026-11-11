import { api } from '@/lib/api'
import type { ClassSummary, CreateClassRequest, CreatedClass } from './contracts'

export const classesKey = ['classes'] as const

export function listClasses(signal?: AbortSignal) {
  return api<ClassSummary[]>('/classes', { signal })
}

export function getClass(classId: string, signal?: AbortSignal) {
  return api<ClassSummary>(`/classes/${encodeURIComponent(classId)}`, { signal })
}

export function createClass(request: CreateClassRequest) {
  return api<CreatedClass>('/classes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}
