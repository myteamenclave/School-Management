import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { GraduationCap } from 'lucide-react'
import { Badge } from '../../../components/ui/badge'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../components/ui/table'
import {
  parentPortalApi,
  PARENT_KEYS,
  type StudentGrade,
} from '../../../api/parentPortal'
import { useParentChildYear } from '../useParentChildYear'
import { ParentChildYearBar } from '../ParentChildYearBar'

const EmptyState = ({ children }: { children: React.ReactNode }) => (
  <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
    {children}
  </div>
)

export function ChildGradesPage() {
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

  const gradesEnabled = !!(childId && academicYearId)

  const {
    data: grades = [],
    isFetching: gradesLoading,
  } = useQuery({
    queryKey: gradesEnabled
      ? PARENT_KEYS.childGrades(childId!, academicYearId!)
      : ['parent', 'grades', 'disabled'],
    queryFn: () => parentPortalApi.getChildGrades(childId!, academicYearId!),
    enabled: gradesEnabled,
  })

  // Group grades by semester (report-card layout), semesters ordered by name.
  const semesterGroups = useMemo(() => {
    const map = new Map<string, StudentGrade[]>()
    for (const g of grades) {
      const list = map.get(g.semesterName) ?? []
      list.push(g)
      map.set(g.semesterName, list)
    }
    return [...map.entries()].sort(([a], [b]) => a.localeCompare(b))
  }, [grades])

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <GraduationCap size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Grades</h1>
          <p className="text-sm text-muted-foreground">Report card — grades by subject and term</p>
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

          {gradesLoading ? (
            <EmptyState>Loading grades…</EmptyState>
          ) : semesterGroups.length === 0 ? (
            <EmptyState>
              No grades have been published for {selectedChild?.studentName ?? 'this student'}
              {selectedYear ? ` in ${selectedYear.name}` : ''} yet.
            </EmptyState>
          ) : (
            <div className="flex flex-col gap-6">
              {semesterGroups.map(([semesterName, rows]) => (
                <div key={semesterName}>
                  <h2 className="mb-2 text-sm font-semibold text-foreground">{semesterName}</h2>
                  <div className="rounded-lg border border-border overflow-x-auto">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Subject</TableHead>
                          <TableHead className="w-24 text-right">Term</TableHead>
                          <TableHead className="w-20">Letter</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {rows.map((g) => (
                          <TableRow key={g.id}>
                            <TableCell className="font-medium">{g.subjectName}</TableCell>
                            <TableCell className="text-right text-sm tabular-nums">
                              {g.termScore ?? '—'}
                            </TableCell>
                            <TableCell>
                              {g.letterGrade ? (
                                <Badge variant="secondary">{g.letterGrade}</Badge>
                              ) : (
                                <span className="text-sm text-muted-foreground">—</span>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}
