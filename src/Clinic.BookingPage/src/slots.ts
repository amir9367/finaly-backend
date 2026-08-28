import type { DayAvailabilityDto } from './types'

/** Expands working-hours-minus-busy into candidate start times (server re-validates). */
export function computeFreeSlots(day: DayAvailabilityDto, visitMinutes: number): string[] {
  const toMinutes = (text: string) => {
    const [h, m] = text.split(':').map(Number)
    return h * 60 + m
  }
  const busyRanges = day.busy.map((w) => [toMinutes(w.from), toMinutes(w.to)] as const)

  const isBusy = (t: number) => {
    const end = t + visitMinutes
    return busyRanges.some(([b0, b1]) => t < b1 && b0 < end)
  }

  // All possible slot start times (free + booked) across working hours, in order
  const allSlots: number[] = []
  for (const window of day.workingHours) {
    for (let t = toMinutes(window.from); t + visitMinutes <= toMinutes(window.to); t += visitMinutes) {
      allSlots.push(t)
    }
  }

  // Group slots by clock hour (10:00, 10:20, 10:40 all belong to hour 10).
  // Rule: always keep at least 1 slot free per hour. Once only 1 free slot remains
  // in an hour (all others are booked), block that last slot too.
  // Works for any visit duration: 5-min → 12 slots/hr, 10-min → 6, 20-min → 3, etc.
  const slotsByHour: Record<number, number[]> = {}
  for (const t of allSlots) {
    const hour = Math.floor(t / 60)
    ;(slotsByHour[hour] ??= []).push(t)
  }

  const blockedByRule = new Set<number>()
  for (const group of Object.values(slotsByHour)) {
    const free = group.filter((t) => !isBusy(t))
    if (free.length === 1) {
      blockedByRule.add(free[0])
    }
  }

  return allSlots
    .filter((t) => !isBusy(t) && !blockedByRule.has(t))
    .map((t) =>
      `${String(Math.floor(t / 60)).padStart(2, '0')}:${String(t % 60).padStart(2, '0')}`
    )
}
