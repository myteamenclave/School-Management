import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip } from 'recharts'
import type { MonthlyAttendancePointDto } from '../../../api/dashboard'
import { CHART_COLORS, monthLabel } from './format'

export function AttendanceChart({ points }: { points: MonthlyAttendancePointDto[] }) {
  const data = points.map((p) => ({
    label: monthLabel(p.year, p.month),
    rate: Math.round(p.presentRate * 100),
    total: p.totalRecords,
  }))

  return (
    <div className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke={CHART_COLORS.grid} vertical={false} />
          <XAxis dataKey="label" tick={{ fontSize: 11, fill: CHART_COLORS.muted }} tickLine={false} axisLine={false} />
          <YAxis
            domain={[0, 100]}
            tick={{ fontSize: 11, fill: CHART_COLORS.muted }}
            tickLine={false}
            axisLine={false}
            width={40}
            tickFormatter={(v: number) => `${v}%`}
          />
          <Tooltip
            formatter={(value, _name, item) => {
              const total = (item as { payload?: { total?: number } })?.payload?.total ?? 0
              return [`${Number(value)}%`, `Present rate (${total} records)`]
            }}
          />
          <Line
            type="monotone"
            dataKey="rate"
            stroke={CHART_COLORS.secondary}
            strokeWidth={2}
            dot={{ r: 3 }}
            activeDot={{ r: 5 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
