export { cn } from "cn"

const bnDigits = ['০', '১', '২', '৩', '৪', '৫', '৬', '৭', '৮', '৯']

export function toBnDigits(val: number | string): string {
  return String(val).replace(/\d/g, (d) => bnDigits[Number(d)] ?? d)
}

