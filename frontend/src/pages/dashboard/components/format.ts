// Design-system brand colors (from index.css @theme) — recharts needs concrete color strings.
export const CHART_COLORS = {
  primary: '#1E3A5F',
  secondary: '#2563EB',
  accent: '#059669',
  destructive: '#DC2626',
  muted: '#64748B',
  grid: '#E4E7EB',
} as const

const moneyFmt = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 })

export function formatMoney(value: number): string {
  return moneyFmt.format(value)
}

export function formatPercent(rate0to1: number): string {
  return `${Math.round(rate0to1 * 100)}%`
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

// Short label for a monthly bucket, e.g. "Oct '25".
export function monthLabel(year: number, month: number): string {
  return `${MONTHS[month - 1]} '${String(year).slice(2)}`
}
