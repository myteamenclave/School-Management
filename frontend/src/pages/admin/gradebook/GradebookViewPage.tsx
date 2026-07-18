import { useState, useEffect, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BookOpen } from 'lucide-react'
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
import { gradebookApi, GRADEBOOK_KEYS } from '../../../api/gradebook'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'
import { subjectsApi, SUBJECT_KEYS } from '../../../api/subjects'

export function GradebookViewPage() {
  const [academicYearId, setAcademicYearId] = useState<string | null>(null)
  const [gradeId, setGradeId] = useState<string | null>(null)
  const [sectionId, setSectionId] = useState<string | null>(null)
  const [subjectId, setSubjectId] = useState<string | null>(null)
  const [semesterId, setSemesterId] = useState<string | null>(null)

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

  useEffect(() => {
    if (years.length > 0 && !academicYearId) {
      const active = years.find((y) => y.isCurrent) ?? years[0]
      setAcademicYearId(active.id)
    }
  }, [years, academicYearId])

  const selectedYear = years.find((y) => y.id === academicYearId)
  const semesters = useMemo(() => selectedYear?.semesters ?? [], [selectedYear])

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

  const totalEntries = roster?.entries.length ?? 0
  const gradedCount = roster?.entries.filter((e) => e.termScore !== null).length ?? 0

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <BookOpen size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Gradebook</h1>
          <p className="text-sm text-muted-foreground">Read-only view of subject grades per term</p>
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
          Select a semester, grade, section, and subject to view grades.
        </div>
      )}

      {rosterEnabled && rosterLoading && (
        <div className="rounded-lg border border-border p-10 text-center text-sm text-muted-foreground">
          Loading…
        </div>
      )}

      {rosterEnabled && !rosterLoading && roster && (
        <>
          <div className="mb-3">
            <p className="text-sm text-muted-foreground">
              {roster.sectionName} — {roster.subjectName} — {roster.semesterName} — {gradedCount} of{' '}
              {totalEntries} graded
            </p>
          </div>

          {totalEntries === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
              No students enrolled in this section for the selected year.
            </div>
          ) : (
            <div className="rounded-lg border border-border overflow-x-auto">
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
                  {roster.entries.map((entry) => (
                    <TableRow key={entry.studentId}>
                      <TableCell className="font-mono text-xs text-muted-foreground">
                        {entry.studentCode}
                      </TableCell>
                      <TableCell className="font-medium">{entry.studentName}</TableCell>
                      <TableCell className="text-sm tabular-nums">{entry.midtermScore ?? '—'}</TableCell>
                      <TableCell className="text-sm tabular-nums">{entry.finalScore ?? '—'}</TableCell>
                      <TableCell className="text-sm tabular-nums">{entry.courseworkScore ?? '—'}</TableCell>
                      <TableCell className="text-sm tabular-nums">{entry.termScore ?? '—'}</TableCell>
                      <TableCell className="text-sm font-medium">{entry.letterGrade ?? '—'}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{entry.notes ?? '—'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </>
      )}
    </div>
  )
}
