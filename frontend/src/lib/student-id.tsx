import React, { createContext, useContext, useState } from 'react'

type StudentIdContextType = {
  studentId: string
  setStudentId: (id: string) => void
}

const StudentIdContext = createContext<StudentIdContextType | undefined>(undefined)

const STORAGE_KEY = 'studentId'

export function StudentIdProvider({ children }: { children: React.ReactNode }) {
  const [studentId, setStudentIdState] = useState<string>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) || ''
    } catch {
      return ''
    }
  })

  const setStudentId = (id: string) => {
    setStudentIdState(id)
    try {
      if (id) {
        localStorage.setItem(STORAGE_KEY, id)
      } else {
        localStorage.removeItem(STORAGE_KEY)
      }
    } catch {
      // ignore storage errors
    }
  }

  return (
    <StudentIdContext.Provider value={{ studentId, setStudentId }}>
      {children}
    </StudentIdContext.Provider>
  )
}

export function useStudentId(): StudentIdContextType {
  const context = useContext(StudentIdContext)
  if (!context) {
    throw new Error('useStudentId must be used within a StudentIdProvider')
  }
  return context
}
