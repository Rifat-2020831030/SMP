import { api } from '@/lib/api'
import type { Hold, HoldStatus, StudentHold } from './contracts'

export function createHold(classId: string, studentId: string) {
  return api<Hold>(`/classes/${encodeURIComponent(classId)}/holds`, {
    method: 'POST',
    headers: {
      'X-Student-Id': studentId,
    },
  })
}

export function getHold(holdId: string, studentId: string, signal?: AbortSignal) {
  return api<HoldStatus>(`/holds/${encodeURIComponent(holdId)}`, {
    method: 'GET',
    headers: {
      'X-Student-Id': studentId,
    },
    signal,
  })
}

export function confirmHold(holdId: string, studentId: string) {
  return api<{ holdId: string; status: string }>(`/holds/${encodeURIComponent(holdId)}/confirm`, {
    method: 'POST',
    headers: {
      'X-Student-Id': studentId,
    },
  })
}

export function cancelHold(holdId: string, studentId: string) {
  return api<void>(`/holds/${encodeURIComponent(holdId)}`, {
    method: 'DELETE',
    headers: {
      'X-Student-Id': studentId,
    },
  })
}

export function getMyHolds(studentId: string, signal?: AbortSignal) {
  return api<StudentHold[]>('/me/holds', {
    method: 'GET',
    headers: {
      'X-Student-Id': studentId,
    },
    signal,
  })
}
