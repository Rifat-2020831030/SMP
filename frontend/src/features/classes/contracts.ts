export type CreateClassRequest = {
  name: string
  totalSeats: number
}

export type CreatedClass = {
  id: string
  name: string
  totalSeats: number
}

export type ClassSummary = CreatedClass & {
  heldSeats: number
  confirmedSeats: number
  availableSeats: number
}
