import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip } from 'recharts'
import type { EnrollmentBreakdownDto } from '../../../api/dashboard'
import { CHART_COLORS } from './format'
import { EmptyTile } from './DashboardCard'

export function EnrollmentBreakdown({ enrollment }: { enrollment: EnrollmentBreakdownDto }) {
  if (enrollment.totalEnrolled === 0) {
    return <EmptyTile message="No students enrolled for this year yet." />
  }

  const data = enrollment.byGrade.map((g) => ({ label: g.gradeName, Students: g.count }))

  return (
    <div>
      <div className="mb-4">
        <p className="font-heading text-3xl font-semibold text-foreground">{enrollment.totalEnrolled}</p>
        <p className="text-xs text-muted-foreground">students enrolled</p>
      </div>

      <div className="h-48 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} layout="vertical" margin={{ top: 0, right: 8, left: 8, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke={CHART_COLORS.grid} horizontal={false} />
            <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: CHART_COLORS.muted }} tickLine={false} axisLine={false} />
            <YAxis
              type="category"
              dataKey="label"
              tick={{ fontSize: 11, fill: CHART_COLORS.muted }}
              tickLine={false}
              axisLine={false}
              width={72}
            />
            <Tooltip />
            <Bar dataKey="Students" fill={CHART_COLORS.secondary} radius={[0, 3, 3, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        {enrollment.byStatus.map((s) => (
          <span
            key={s.status}
            className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs text-muted-foreground"
          >
            <span className="font-medium text-foreground">{s.count}</span> {s.status}
          </span>
        ))}
      </div>
    </div>
  )
}
