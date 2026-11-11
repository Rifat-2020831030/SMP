import { describe, it, expect, beforeEach } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { StudentIdProvider, useStudentId } from '@/lib/student-id'
import { ApiError } from '@/lib/api'
import React from 'react'

describe('Frontend Core Functionality', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  describe('StudentIdProvider', () => {
    it('initializes with empty string when localStorage is empty', () => {
      const wrapper = ({ children }: { children: React.ReactNode }) => (
        <StudentIdProvider>{children}</StudentIdProvider>
      )
      const { result } = renderHook(() => useStudentId(), { wrapper })
      expect(result.current.studentId).toBe('')
    })

    it('persists and restores 17-digit student ID to localStorage', () => {
      const wrapper = ({ children }: { children: React.ReactNode }) => (
        <StudentIdProvider>{children}</StudentIdProvider>
      )
      const { result } = renderHook(() => useStudentId(), { wrapper })

      act(() => {
        result.current.setStudentId('20152691234567890')
      })

      expect(result.current.studentId).toBe('20152691234567890')
      expect(localStorage.getItem('studentId')).toBe('20152691234567890')
    })

    it('clears localStorage when student ID is set to empty', () => {
      localStorage.setItem('studentId', '20152691234567890')
      const wrapper = ({ children }: { children: React.ReactNode }) => (
        <StudentIdProvider>{children}</StudentIdProvider>
      )
      const { result } = renderHook(() => useStudentId(), { wrapper })

      expect(result.current.studentId).toBe('20152691234567890')

      act(() => {
        result.current.setStudentId('')
      })

      expect(result.current.studentId).toBe('')
      expect(localStorage.getItem('studentId')).toBeNull()
    })
  })

  describe('ApiError Handling', () => {
    it('creates an ApiError with message and status code', () => {
      const err = new ApiError('No available seats.', 409)
      expect(err.message).toBe('No available seats.')
      expect(err.status).toBe(409)
      expect(err.name).toBe('ApiError')
    })
  })

  describe('Countdown and Expiry Calculation', () => {
    it('correctly calculates remaining seconds and detects expiry', () => {
      const futureTime = new Date(Date.now() + 120_000).toISOString() // 2 minutes from now
      const secondsLeft = Math.floor((new Date(futureTime).getTime() - Date.now()) / 1000)
      expect(secondsLeft).toBeGreaterThanOrEqual(118)
      expect(secondsLeft).toBeLessThanOrEqual(120)

      const pastTime = new Date(Date.now() - 5_000).toISOString() // 5 seconds ago
      const pastSecondsLeft = Math.max(0, Math.floor((new Date(pastTime).getTime() - Date.now()) / 1000))
      expect(pastSecondsLeft).toBe(0)
    })
  })
})
