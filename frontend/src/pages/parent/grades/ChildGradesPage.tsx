import { useState, useEffect, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { GraduationCap } from 'lucide-react'
import { Label } from '../../../components/ui/label'
import { Badge } from '../../../components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select'
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

const EmptyState = ({ children }: { children: React.ReactNode }) => (
  <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
    {children}
  </div>
)

export function ChildGradesPage() {
  const [childId, setChildId] = useState<string | null>(null)
  const [academicYearId, setAcademicYearId] = useState<string | null>(null)

  const {
    data: children = [],
    isLoading: childrenLoading,
    isError: childrenError,
  } = useQuery({
    queryKey: PARENT_KEYS.children(),
    queryFn: parentPortalApi.getChildren,
  })

  const { data: years = [] } = useQuery({
    queryKey: PARENT_KEYS.academicYears(),
    queryFn: parentPortalApi.getAcademicYears,
  })

  // Auto-select the only child; keep selection valid as the list resolves.
  useEffect(() => {
    if (children.length === 0) return
    setChildId((prev) => (children.some((c) => c.studentId === prev) ? prev : children[0].studentId))
  }, [children])

  // Default to the current academic year.
  useEffect(() => {
    if (years.length === 0) return
    const current = years.find((y) => y.isCurrent) ?? years[0]
    setAcademicYearId((prev) => (years.some((y) => y.id === prev) ? prev : current.id))
  }, [years])

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

  const selectedChild = children.find((c) => c.studentId === childId)
  const selectedYear = years.find((y) => y.id === academicYearId)

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
          <h1 className="text-xl font-semibold text-foreground">My Children</h1>
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
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 mb-6">
            {/* Child switcher — hidden when there's only one child. */}
            {children.length > 1 && (
              <div className="flex flex-col gap-1.5">
                <Label>Child</Label>
                <Select value={childId ?? ''} onValueChange={setChildId}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select child" />
                  </SelectTrigger>
                  <SelectContent>
                    {children.map((c) => (
                      <SelectItem key={c.studentId} value={c.studentId}>
                        {c.studentName} ({c.studentCode})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <Label>Academic Year</Label>
              <Select value={academicYearId ?? ''} onValueChange={setAcademicYearId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select year" />
                </SelectTrigger>
                <SelectContent>
                  {years.map((y) => (
                    <SelectItem key={y.id} value={y.id}>
                      {y.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {/* Selected child summary. */}
          {selectedChild && (
            <div className="mb-4 flex flex-wrap items-center gap-2">
              <span className="text-base font-semibold text-foreground">{selectedChild.studentName}</span>
              <span className="font-mono text-xs text-muted-foreground">{selectedChild.studentCode}</span>
              {selectedChild.currentGradeLabel && (
                <Badge variant="secondary">
                  {selectedChild.currentGradeLabel}
                  {selectedChild.currentSectionName ? ` · ${selectedChild.currentSectionName}` : ''}
                </Badge>
              )}
              {selectedChild.enrollmentStatus !== 'Active' && (
                <Badge variant="outline">{selectedChild.enrollmentStatus}</Badge>
              )}
            </div>
          )}

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
