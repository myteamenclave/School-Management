import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { CalendarDays } from 'lucide-react'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../components/ui/table'
import {
  AttendanceStatusBadge,
  ATTENDANCE_STATUS_TEXT_COLORS,
} from '../../../components/shared/AttendanceStatusBadge'
import { parentPortalApi, PARENT_KEYS } from '../../../api/parentPortal'
import { useParentChildYear } from '../useParentChildYear'
import { ParentChildYearBar } from '../ParentChildYearBar'

const EmptyState = ({ children }: { children: React.ReactNode }) => (
  <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
    {children}
  </div>
)

// One summary stat. The four status counts reuse the shared status colours.
function StatCard({ label, value, className }: { label: string; value: string | number; className?: string }) {
  return (
    <div className="rounded-lg border border-border p-4 text-center">
      <div className={`text-2xl font-semibold tabular-nums ${className ?? 'text-foreground'}`}>{value}</div>
      <div className="mt-1 text-xs text-muted-foreground">{label}</div>
    </div>
  )
}

function formatDate(iso: string) {
  const d = new Date(iso + 'T00:00:00')
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export function ChildAttendancePage() {
  const {
    children,
    years,
    childId,
    setChildId,
    academicYearId,
    setAcademicYearId,
    childrenLoading,
    childrenError,
    selectedChild,
    selectedYear,
  } = useParentChildYear()

  const enabled = !!(childId && academicYearId)

  const { data, isFetching } = useQuery({
    queryKey: enabled
      ? PARENT_KEYS.childAttendance(childId!, academicYearId!)
      : ['parent', 'attendance', 'disabled'],
    queryFn: () => parentPortalApi.getChildAttendance(childId!, academicYearId!),
    enabled,
  })

  const summary = data?.summary
  // Reverse-chronological — sort in the UI, don't rely on server ordering.
  const entries = useMemo(
    () => [...(data?.entries ?? [])].sort((a, b) => b.date.localeCompare(a.date)),
    [data],
  )

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <CalendarDays size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Attendance</h1>
          <p className="text-sm text-muted-foreground">Daily attendance record by academic year</p>
        </div>
      </div>

      {childrenLoading ? (
        <EmptyState>Loading…</EmptyState>
      ) : childrenError ? (
        <EmptyState>Something went wrong loading your children. Please refresh to try again.</EmptyState>
      ) : children.length === 0 ? (
        <EmptyState>
          No students are linked to your account yet. Please contact the school office.
        </EmptyState>
      ) : (
        <>
          <ParentChildYearBar
            children={children}
            years={years}
            childId={childId}
            academicYearId={academicYearId}
            onChildChange={setChildId}
            onYearChange={setAcademicYearId}
          />

          {isFetching ? (
            <EmptyState>Loading attendance…</EmptyState>
          ) : !summary || summary.totalMarked === 0 ? (
            <EmptyState>
              No attendance has been recorded for {selectedChild?.studentName ?? 'this student'}
              {selectedYear ? ` in ${selectedYear.name}` : ''} yet.
            </EmptyState>
          ) : (
            <>
              {/* Summary hero — rate + the four status counts. */}
              <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-5">
                <StatCard
                  label={`Attendance · ${summary.totalMarked} marked days`}
                  value={summary.attendanceRate === null ? '—' : `${summary.attendanceRate}%`}
                  className="text-primary"
                />
                <StatCard label="Present" value={summary.presentCount} className={ATTENDANCE_STATUS_TEXT_COLORS.Present} />
                <StatCard label="Late" value={summary.lateCount} className={ATTENDANCE_STATUS_TEXT_COLORS.Late} />
                <StatCard label="Absent" value={summary.absentCount} className={ATTENDANCE_STATUS_TEXT_COLORS.Absent} />
                <StatCard label="Excused" value={summary.excusedCount} className={ATTENDANCE_STATUS_TEXT_COLORS.Excused} />
              </div>

              {/* Daily log — reverse-chronological. */}
              <div className="rounded-lg border border-border overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-40">Date</TableHead>
                      <TableHead className="w-28">Status</TableHead>
                      <TableHead>Section</TableHead>
                      <TableHead>Notes</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {entries.map((e) => (
                      <TableRow key={e.id}>
                        <TableCell className="text-sm tabular-nums">{formatDate(e.date)}</TableCell>
                        <TableCell>
                          <AttendanceStatusBadge status={e.status} />
                        </TableCell>
                        <TableCell className="text-sm">{e.sectionName}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {e.notes || '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </>
          )}
        </>
      )}
    </div>
  )
}
