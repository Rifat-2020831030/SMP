export type Hold = {
  holdId: string
  classId: string
  studentId: string
  status: 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED'
  expiresAt: string // ISO string
}

export type HoldStatus = {
  holdId: string
  classId: string
  status: 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED'
  expiresAt: string
}

export type StudentHold = {
  holdId: string
  classId: string
  className: string
  status: 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED'
  expiresAt: string
}
