import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend } from 'recharts'
import type { MonthlyMoneyPointDto } from '../../../api/dashboard'
import { CHART_COLORS, formatMoney, monthLabel } from './format'

export function FinanceChart({ points }: { points: MonthlyMoneyPointDto[] }) {
  const data = points.map((p) => ({
    label: monthLabel(p.year, p.month),
    Billed: p.billed,
    Collected: p.collected,
  }))

  return (
    <div className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke={CHART_COLORS.grid} vertical={false} />
          <XAxis dataKey="label" tick={{ fontSize: 11, fill: CHART_COLORS.muted }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fontSize: 11, fill: CHART_COLORS.muted }}
            tickLine={false}
            axisLine={false}
            width={48}
            tickFormatter={(v: number) => formatMoney(v)}
          />
          <Tooltip formatter={(value) => formatMoney(Number(value))} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
          <Bar dataKey="Billed" fill={CHART_COLORS.primary} radius={[3, 3, 0, 0]} />
          <Bar dataKey="Collected" fill={CHART_COLORS.accent} radius={[3, 3, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
