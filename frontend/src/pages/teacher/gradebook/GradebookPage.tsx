import { useState, useEffect, useMemo } from 'react'
import { useBlocker } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { BookOpen } from 'lucide-react'
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
import { gradebookApi, gradeScaleApi, GRADEBOOK_KEYS } from '../../../api/gradebook'
import type { GradeRosterEntry, GradeScaleBand } from '../../../api/gradebook'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'
import { subjectsApi, SUBJECT_KEYS } from '../../../api/subjects'

// Provisional preview weights — mirror the server GradeWeights (30/40/30).
// The server recomputes the authoritative TermScore + LetterGrade on save.
const WEIGHTS = { midterm: 0.3, final: 0.4, coursework: 0.3 }

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

function parseScore(value: string): number | null {
  if (value.trim() === '') return null
  const n = Number(value)
  return Number.isFinite(n) ? n : null
}

function provisionalTerm(mid: number | null, fin: number | null, course: number | null): number | null {
  if (mid === null || fin === null || course === null) return null
  return Math.round((mid * WEIGHTS.midterm + fin * WEIGHTS.final + course * WEIGHTS.coursework) * 100) / 100
}

// Mirrors the server LetterResolver: first band whose [min, max] contains the score.
function resolveLetter(bands: GradeScaleBand[], score: number | null): string | null {
  if (score === null) return null
  return bands.find((b) => score >= b.minScore && score <= b.maxScore)?.letter ?? null
}

interface EntryState {
  studentId: string
  studentName: string
  studentCode: string
  midterm: string
  final: string
  coursework: string
  notes: string
}

export function GradebookPage() {
  const [academicYearId, setAcademicYearId] = useState<string | null>(null)
  const [gradeId, setGradeId] = useState<string | null>(null)
  const [sectionId, setSectionId] = useState<string | null>(null)
  const [subjectId, setSubjectId] = useState<string | null>(null)
  const [semesterId, setSemesterId] = useState<string | null>(null)
  const [entries, setEntries] = useState<EntryState[]>([])
  const [savedEntries, setSavedEntries] = useState<EntryState[]>([])
  const [isDirty, setIsDirty] = useState(false)

  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
  })

  const { data: grades = [] } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
  })

  const { data: subjectsPage } = useQuery({
    queryKey: SUBJECT_KEYS.list({ isActive: true, search: '', page: 1, pageSize: 200 }),
    queryFn: () => subjectsApi.list({ isActive: true, search: '', page: 1, pageSize: 200 }),
  })
  const subjects = subjectsPage?.items ?? []

  // Grade scale bands — used to map the provisional term score to a letter live,
  // matching the server's mapping (readable by teachers).
  const { data: bands = [] } = useQuery({
    queryKey: GRADEBOOK_KEYS.scale,
    queryFn: gradeScaleApi.getAll,
  })

  // Default to active year, and its current semester.
  useEffect(() => {
    if (years.length > 0 && !academicYearId) {
      const active = years.find((y) => y.isCurrent) ?? years[0]
      setAcademicYearId(active.id)
    }
  }, [years, academicYearId])

  const selectedYear = years.find((y) => y.id === academicYearId)
  const semesters = useMemo(() => selectedYear?.semesters ?? [], [selectedYear])

  // Default semester to the current one when the year changes.
  useEffect(() => {
    if (semesters.length > 0) {
      const current = semesters.find((s) => s.isCurrent) ?? semesters[0]
      setSemesterId((prev) => (semesters.some((s) => s.id === prev) ? prev : current.id))
    } else {
      setSemesterId(null)
    }
  }, [semesters])

  useEffect(() => {
    setSectionId(null)
  }, [gradeId])

  const selectedGrade = grades.find((g) => g.id === gradeId)
  const sections = selectedGrade?.sections ?? []

  const rosterEnabled = !!(sectionId && subjectId && semesterId)

  const { data: roster, isFetching: rosterLoading } = useQuery({
    queryKey: rosterEnabled
      ? GRADEBOOK_KEYS.subjectRoster(sectionId!, subjectId!, semesterId!)
      : ['gradebook', 'disabled'],
    queryFn: () => gradebookApi.getSubjectRoster(sectionId!, subjectId!, semesterId!),
    enabled: rosterEnabled,
  })

  useEffect(() => {
    if (!roster) return
    const loaded: EntryState[] = roster.entries.map((e: GradeRosterEntry) => ({
      studentId: e.studentId,
      studentName: e.studentName,
      studentCode: e.studentCode,
      midterm: e.midtermScore?.toString() ?? '',
      final: e.finalScore?.toString() ?? '',
      coursework: e.courseworkScore?.toString() ?? '',
      notes: e.notes ?? '',
    }))
    setEntries(loaded)
    setSavedEntries(loaded)
    setIsDirty(false)
  }, [roster])

  useEffect(() => {
    if (savedEntries.length === 0) {
      setIsDirty(false)
      return
    }
    setIsDirty(JSON.stringify(entries) !== JSON.stringify(savedEntries))
  }, [entries, savedEntries])

  const blocker = useBlocker(isDirty)

  const submitMutation = useMutation({
    mutationFn: () =>
      gradebookApi.bulkUpsert({
        sectionId: sectionId!,
        subjectId: subjectId!,
        semesterId: semesterId!,
        entries: entries.map((e) => ({
          studentId: e.studentId,
          midterm: parseScore(e.midterm),
          final: parseScore(e.final),
          coursework: parseScore(e.coursework),
          notes: e.notes || null,
        })),
      }),
    onSuccess: (result) => {
      toast.success(`Grades saved — ${result.upserted} record(s) upserted.`)
      // Recompute saved snapshot with provisional term so the row reflects the save;
      // the query refetch below replaces it with the authoritative server values.
      setSavedEntries([...entries])
      setIsDirty(false)
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const updateEntry = (studentId: string, field: 'midterm' | 'final' | 'coursework' | 'notes', value: string) => {
    setEntries((prev) =>
      prev.map((e) => (e.studentId === studentId ? { ...e, [field]: value } : e))
    )
  }

  const scoreInvalid = (v: string) => {
    if (v.trim() === '') return false
    const n = Number(v)
    return !Number.isFinite(n) || n < 0 || n > 100
  }

  const anyInvalid = entries.some(
    (e) => scoreInvalid(e.midterm) || scoreInvalid(e.final) || scoreInvalid(e.coursework)
  )

  const canSubmit = rosterEnabled && entries.length > 0 && !anyInvalid && !submitMutation.isPending

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <BookOpen size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Gradebook</h1>
          <p className="text-sm text-muted-foreground">
            Enter Midterm, Final, and Coursework scores per subject and term
          </p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-5 mb-6">
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
          <Label>Semester</Label>
          <Select value={semesterId ?? ''} onValueChange={setSemesterId} disabled={!academicYearId}>
            <SelectTrigger>
              <SelectValue placeholder="Select term" />
            </SelectTrigger>
            <SelectContent>
              {semesters.map((s) => (
                <SelectItem key={s.id} value={s.id}>
                  {s.name}
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
          <Select value={sectionId ?? ''} onValueChange={setSectionId} disabled={!gradeId}>
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
          <Label>Subject</Label>
          <Select value={subjectId ?? ''} onValueChange={setSubjectId}>
            <SelectTrigger>
              <SelectValue placeholder="Select subject" />
            </SelectTrigger>
            <SelectContent>
              {subjects.map((s) => (
                <SelectItem key={s.id} value={s.id}>
                  {s.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {!rosterEnabled && (
        <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
          Select a semester, grade, section, and subject to load the grade roster.
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
                  {roster.sectionName} — {roster.subjectName} — {roster.semesterName} — {entries.length}{' '}
                  student(s)
                </p>
                <p className="text-xs text-muted-foreground">
                  Term = 30% Midterm + 40% Final + 30% Coursework
                </p>
              </div>

              <div className="rounded-lg border border-border overflow-x-auto mb-4">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-28">Code</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead className="w-24">Midterm</TableHead>
                      <TableHead className="w-24">Final</TableHead>
                      <TableHead className="w-24">Coursework</TableHead>
                      <TableHead className="w-20">Term</TableHead>
                      <TableHead className="w-16">Letter</TableHead>
                      <TableHead>Notes</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {entries.map((entry) => {
                      const term = provisionalTerm(
                        parseScore(entry.midterm),
                        parseScore(entry.final),
                        parseScore(entry.coursework)
                      )
                      // Resolve the letter live from the same bands the server uses,
                      // so it updates as scores are typed (not just after save).
                      const letter = resolveLetter(bands, term)
                      return (
                        <TableRow key={entry.studentId}>
                          <TableCell className="font-mono text-xs text-muted-foreground">
                            {entry.studentCode}
                          </TableCell>
                          <TableCell className="font-medium">{entry.studentName}</TableCell>
                          {(['midterm', 'final', 'coursework'] as const).map((field) => (
                            <TableCell key={field}>
                              <Input
                                type="number"
                                min={0}
                                max={100}
                                step={0.01}
                                className={`h-8 text-xs ${scoreInvalid(entry[field]) ? 'border-red-500' : ''}`}
                                placeholder="—"
                                value={entry[field]}
                                onChange={(e) => updateEntry(entry.studentId, field, e.target.value)}
                              />
                            </TableCell>
                          ))}
                          <TableCell className="text-sm tabular-nums">
                            {term !== null ? term : <span className="text-muted-foreground">—</span>}
                          </TableCell>
                          <TableCell className="text-sm font-medium">
                            {letter ?? <span className="text-muted-foreground">—</span>}
                          </TableCell>
                          <TableCell>
                            <Input
                              className="h-8 text-xs"
                              placeholder="Optional notes…"
                              value={entry.notes}
                              maxLength={500}
                              onChange={(e) => updateEntry(entry.studentId, 'notes', e.target.value)}
                            />
                          </TableCell>
                        </TableRow>
                      )
                    })}
                  </TableBody>
                </Table>
              </div>

              <div className="flex items-center justify-end gap-3">
                {anyInvalid && (
                  <p className="text-xs text-red-500">Scores must be between 0 and 100.</p>
                )}
                <Button onClick={() => submitMutation.mutate()} disabled={!canSubmit}>
                  {submitMutation.isPending ? 'Saving…' : 'Save Grades'}
                </Button>
              </div>
            </>
          )}
        </>
      )}

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
            You have unsaved grade changes. If you leave now, they will be lost.
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
