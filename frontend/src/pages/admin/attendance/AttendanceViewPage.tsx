import { useState, useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ClipboardList } from 'lucide-react'
import { Label } from '../../../components/ui/label'
import { Input } from '../../../components/ui/input'
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
import { attendanceApi, ATTENDANCE_KEYS } from '../../../api/attendance'
import type { AttendanceStatus } from '../../../api/attendance'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'

const STATUS_COLORS: Record<AttendanceStatus, string> = {
  Present: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Late: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
  Absent: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  Excused: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
}

function todayString(): string {
  return new Date().toISOString().split('T')[0]
}

export function AttendanceViewPage() {
  const [gradeId, setGradeId] = useState<string | null>(null)
  const [sectionId, setSectionId] = useState<string | null>(null)
  const [academicYearId, setAcademicYearId] = useState<string | null>(null)
  const [date, setDate] = useState<string>(todayString())

  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
  })

  const { data: grades = [] } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
  })

  // Default to active year
  useEffect(() => {
    if (years.length > 0 && !academicYearId) {
      const active = years.find((y) => y.isCurrent) ?? years[0]
      setAcademicYearId(active.id)
    }
  }, [years, academicYearId])

  // Reset section when grade changes
  useEffect(() => {
    setSectionId(null)
  }, [gradeId])

  const selectedGrade = grades.find((g) => g.id === gradeId)
  const sections = selectedGrade?.sections ?? []

  const rosterEnabled = !!(sectionId && academicYearId && date)

  const { data: roster, isFetching: rosterLoading } = useQuery({
    queryKey: rosterEnabled
      ? ATTENDANCE_KEYS.sectionRoster(sectionId!, date, academicYearId!)
      : ['attendance', 'disabled'],
    queryFn: () => attendanceApi.getSectionRoster(sectionId!, date, academicYearId!),
    enabled: rosterEnabled,
  })

  const markedEntries = roster?.entries.filter((e) => e.status !== null) ?? []
  const totalEntries = roster?.entries.length ?? 0

  return (
    <div className="p-6 max-w-5xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <ClipboardList size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Attendance</h1>
          <p className="text-sm text-muted-foreground">Read-only view of attendance records</p>
        </div>
      </div>

      {/* Filters */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4 mb-6">
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

        <div className="flex flex-col gap-1.5">
          <Label>Grade</Label>
          <Select value={gradeId ?? ''} onValueChange={setGradeId}>
            <SelectTrigger>
              <SelectValue placeholder="Select grade" />
            </SelectTrigger>
            <SelectContent>
              {grades.map((g) => (
                <SelectItem key={g.id} value={g.id}>
                  {g.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>Section</Label>
          <Select
            value={sectionId ?? ''}
            onValueChange={setSectionId}
            disabled={!gradeId}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select section" />
            </SelectTrigger>
            <SelectContent>
              {sections.map((s) => (
                <SelectItem key={s.id} value={s.id}>
                  {s.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>Date</Label>
          <Input
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
        </div>
      </div>

      {/* Roster */}
      {!rosterEnabled && (
        <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
          Select a year, grade, section, and date to view attendance.
        </div>
      )}

      {rosterEnabled && rosterLoading && (
        <div className="rounded-lg border border-border p-10 text-center text-sm text-muted-foreground">
          Loading…
        </div>
      )}

      {rosterEnabled && !rosterLoading && roster && (
        <>
          <div className="flex items-center justify-between mb-3">
            <p className="text-sm text-muted-foreground">
              {roster.sectionName} — {date} — {markedEntries.length} of {totalEntries} marked
            </p>
          </div>

          {totalEntries === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
              No students enrolled in this section for the selected year.
            </div>
          ) : (
            <div className="rounded-lg border border-border overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-32">Code</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead className="w-32">Status</TableHead>
                    <TableHead>Notes</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {roster.entries.map((entry) => (
                    <TableRow key={entry.studentId}>
                      <TableCell className="font-mono text-xs text-muted-foreground">
                        {entry.studentCode}
                      </TableCell>
                      <TableCell className="font-medium">{entry.studentName}</TableCell>
                      <TableCell>
                        {entry.status ? (
                          <span
                            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[entry.status as AttendanceStatus]}`}
                          >
                            {entry.status}
                          </span>
                        ) : (
                          <span className="text-xs text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {entry.notes ?? '—'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          {markedEntries.length === 0 && totalEntries > 0 && (
            <p className="mt-3 text-sm text-muted-foreground text-center">
              No attendance recorded for this date.
            </p>
          )}
        </>
      )}
    </div>
  )
}
