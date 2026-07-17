import { useState, useEffect } from 'react'
import { useBlocker } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { ClipboardList } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
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
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/dialog'
import { attendanceApi, ATTENDANCE_KEYS, ATTENDANCE_STATUSES } from '../../../api/attendance'
import type { AttendanceStatus, AttendanceRosterEntry } from '../../../api/attendance'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'

const STATUS_COLORS: Record<AttendanceStatus, string> = {
  Present: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Late: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
  Absent: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  Excused: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

function todayString(): string {
  return new Date().toISOString().split('T')[0]
}

interface EntryState {
  studentId: string
  studentName: string
  studentCode: string
  status: AttendanceStatus | null
  notes: string
}

export function AttendancePage() {
  const [gradeId, setGradeId] = useState<string | null>(null)
  const [sectionId, setSectionId] = useState<string | null>(null)
  const [academicYearId, setAcademicYearId] = useState<string | null>(null)
  const [date, setDate] = useState<string>(todayString())
  const [entries, setEntries] = useState<EntryState[]>([])
  const [savedEntries, setSavedEntries] = useState<EntryState[]>([])
  const [isDirty, setIsDirty] = useState(false)

  // Load academic years — default to active year
  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
  })

  // Load grades
  const { data: grades = [] } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
  })

  // Default to active year when years load
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

  // Fetch roster
  const { data: roster, isFetching: rosterLoading } = useQuery({
    queryKey: rosterEnabled
      ? ATTENDANCE_KEYS.sectionRoster(sectionId!, date, academicYearId!)
      : ['attendance', 'disabled'],
    queryFn: () => attendanceApi.getSectionRoster(sectionId!, date, academicYearId!),
    enabled: rosterEnabled,
  })

  // Sync roster into local state
  useEffect(() => {
    if (!roster) return
    const loaded: EntryState[] = roster.entries.map((e: AttendanceRosterEntry) => ({
      studentId: e.studentId,
      studentName: e.studentName,
      studentCode: e.studentCode,
      status: e.status,
      notes: e.notes ?? '',
    }))
    setEntries(loaded)
    setSavedEntries(loaded)
    setIsDirty(false)
  }, [roster])

  // Track dirty state
  useEffect(() => {
    if (savedEntries.length === 0) {
      setIsDirty(false)
      return
    }
    const dirty = JSON.stringify(entries) !== JSON.stringify(savedEntries)
    setIsDirty(dirty)
  }, [entries, savedEntries])

  // Blocker for unsaved changes
  const blocker = useBlocker(isDirty)

  const submitMutation = useMutation({
    mutationFn: () =>
      attendanceApi.bulkUpsert({
        sectionId: sectionId!,
        academicYearId: academicYearId!,
        date,
        entries: entries
          .filter((e) => e.status !== null)
          .map((e) => ({
            studentId: e.studentId,
            status: e.status as AttendanceStatus,
            notes: e.notes || null,
          })),
      }),
    onSuccess: (result) => {
      toast.success(`Attendance saved — ${result.upserted} record(s) upserted.`)
      setSavedEntries([...entries])
      setIsDirty(false)
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleMarkAllPresent = () => {
    setEntries((prev) => prev.map((e) => ({ ...e, status: 'Present' as AttendanceStatus })))
  }

  const updateEntry = (studentId: string, field: 'status' | 'notes', value: string) => {
    setEntries((prev) =>
      prev.map((e) =>
        e.studentId === studentId
          ? { ...e, [field]: field === 'status' ? (value as AttendanceStatus) : value }
          : e
      )
    )
  }

  const canSubmit =
    rosterEnabled &&
    entries.length > 0 &&
    entries.every((e) => e.status !== null) &&
    !submitMutation.isPending

  return (
    <div className="p-6 max-w-5xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <ClipboardList size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Attendance</h1>
          <p className="text-sm text-muted-foreground">Mark daily roll call for your sections</p>
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
          Select a year, grade, section, and date to load the attendance roster.
        </div>
      )}

      {rosterEnabled && rosterLoading && (
        <div className="rounded-lg border border-border p-10 text-center text-sm text-muted-foreground">
          Loading roster…
        </div>
      )}

      {rosterEnabled && !rosterLoading && roster && (
        <>
          {entries.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
              No students enrolled in this section for the selected year.
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between mb-3">
                <p className="text-sm text-muted-foreground">
                  {roster.sectionName} — {date} — {entries.length} student(s)
                </p>
                <Button variant="outline" size="sm" onClick={handleMarkAllPresent}>
                  Mark All Present
                </Button>
              </div>

              <div className="rounded-lg border border-border overflow-hidden mb-4">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-32">Code</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead className="w-44">Status</TableHead>
                      <TableHead>Notes</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {entries.map((entry) => (
                      <TableRow key={entry.studentId}>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {entry.studentCode}
                        </TableCell>
                        <TableCell className="font-medium">{entry.studentName}</TableCell>
                        <TableCell>
                          <Select
                            value={entry.status ?? ''}
                            onValueChange={(val) =>
                              updateEntry(entry.studentId, 'status', val)
                            }
                          >
                            <SelectTrigger className="h-8 text-xs">
                              <SelectValue placeholder="— pick —" />
                            </SelectTrigger>
                            <SelectContent>
                              {ATTENDANCE_STATUSES.map((s) => (
                                <SelectItem key={s} value={s}>
                                  <span
                                    className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[s]}`}
                                  >
                                    {s}
                                  </span>
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell>
                          <Input
                            className="h-8 text-xs"
                            placeholder="Optional notes…"
                            value={entry.notes}
                            maxLength={500}
                            onChange={(e) =>
                              updateEntry(entry.studentId, 'notes', e.target.value)
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              <div className="flex justify-end">
                <Button
                  onClick={() => submitMutation.mutate()}
                  disabled={!canSubmit}
                >
                  {submitMutation.isPending ? 'Saving…' : 'Save Attendance'}
                </Button>
              </div>
            </>
          )}
        </>
      )}

      {/* Unsaved changes blocker dialog */}
      <Dialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open && blocker.state === 'blocked') blocker.reset?.()
        }}
      >
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Discard unsaved changes?</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            You have unsaved attendance changes. If you leave now, they will be lost.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => blocker.reset?.()}>
              Stay
            </Button>
            <Button variant="destructive" onClick={() => blocker.proceed?.()}>
              Discard
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
