export const appointmentReasons = ['General consultation', 'Grooming', 'Bath', 'Vaccination', 'Deworming', 'Surgery', 'Follow-up']

export function clinicDateTime(instant: string | Date, timeZone: string) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(instant))
  const part = (name: string) => parts.find(item => item.type === name)!.value
  return { date: `${part('year')}-${part('month')}-${part('day')}`, time: `${part('hour')}:${part('minute')}` }
}
