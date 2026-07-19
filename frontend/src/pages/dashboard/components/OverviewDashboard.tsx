import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { dashboardApi, DASHBOARD_KEYS } from '../../../api/dashboard'
import { DashboardCard, EmptyTile } from './DashboardCard'
import { YearSelector } from './YearSelector'
import { FinanceTiles } from './FinanceTiles'
import { FinanceChart } from './FinanceChart'
import { AttendanceChart } from './AttendanceChart'
import { EnrollmentBreakdown } from './EnrollmentBreakdown'
import { TeacherCoverage } from './TeacherCoverage'

function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse rounded-lg bg-muted ${className ?? ''}`} />
}

function hasAny<T extends { totalRecords?: number; billed?: number; collected?: number }>(
  points: T[],
  keys: (keyof T)[],
): boolean {
  return points.some((p) => keys.some((k) => Number(p[k]) > 0))
}

export function OverviewDashboard({ displayName }: { displayName?: string }) {
  const [selectedYearId, setSelectedYearId] = useState<string | null>(null)

  const { data: years = [], isLoading: yearsLoading } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
    staleTime: Infinity,
  })

  // Default to the current year once the list arrives.
  useEffect(() => {
    if (selectedYearId === null && years.length > 0) {
      setSelectedYearId(years.find((y) => y.isCurrent)?.id ?? years[0].id)
    }
  }, [years, selectedYearId])

  const {
    data: overview,
    isLoading: overviewLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: DASHBOARD_KEYS.overview(selectedYearId),
    queryFn: () => dashboardApi.overview(selectedYearId ?? undefined),
    enabled: selectedYearId !== null,
  })

  const loading = yearsLoading || overviewLoading || (!overview && !isError)
  const noYears = !yearsLoading && years.length === 0

  return (
    <div className="px-8 py-8 max-w-7xl mx-auto">
      <div className="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">
            {displayName ? `Welcome, ${displayName}` : 'Overview'}
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            School health at a glance — finance, attendance, enrollment, and staffing.
          </p>
        </div>
        {years.length > 0 && (
          <YearSelector years={years} value={selectedYearId} onChange={setSelectedYearId} />
        )}
      </div>

      {noYears && (
        <EmptyTile message="No academic years exist yet. Create one to see the dashboard." />
      )}

      {isError && !noYears && (
        <div className="flex flex-col items-center justify-center h-48 gap-3 text-sm">
          <span className="text-destructive">Failed to load the dashboard.</span>
          <button
            onClick={() => refetch()}
            className="rounded-md border border-border px-3 py-1.5 text-foreground hover:bg-muted"
          >
            Retry
          </button>
        </div>
      )}

      {loading && !noYears && !isError && (
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </div>
          <Skeleton className="h-72" />
          <div className="grid gap-4 lg:grid-cols-2">
            <Skeleton className="h-72" />
            <Skeleton className="h-72" />
          </div>
        </div>
      )}

      {!loading && !isError && overview && (
        <div className="space-y-4">
          <FinanceTiles finance={overview.finance} academicYearId={overview.academicYearId} />

          <DashboardCard
            title="Billed vs. collected"
            description="Monthly — billed by due date, collected by payment date."
            to={`/admin/fee-invoices?academicYearId=${overview.academicYearId}`}
            linkLabel="Invoices"
          >
            {hasAny(overview.financeMonthly, ['billed', 'collected']) ? (
              <FinanceChart points={overview.financeMonthly} />
            ) : (
              <EmptyTile message="No issued invoices for this year yet." />
            )}
          </DashboardCard>

          <div className="grid gap-4 lg:grid-cols-2">
            <DashboardCard
              title="Attendance"
              description="Monthly present rate (present + late)."
              to="/admin/attendance"
              linkLabel="Attendance"
            >
              {hasAny(overview.attendanceMonthly, ['totalRecords']) ? (
                <AttendanceChart points={overview.attendanceMonthly} />
              ) : (
                <EmptyTile message="No attendance recorded for this year yet." />
              )}
            </DashboardCard>

            <DashboardCard
              title="Enrollment"
              description="Current placement by grade and status."
              to="/admin/students"
              linkLabel="Students"
            >
              <EnrollmentBreakdown enrollment={overview.enrollment} />
            </DashboardCard>
          </div>

          <DashboardCard
            title="Teacher coverage"
            description="Staffing snapshot and coverage gaps."
            to="/admin/teachers"
            linkLabel="Teachers"
          >
            <TeacherCoverage teachers={overview.teachers} />
          </DashboardCard>
        </div>
      )}
    </div>
  )
}
