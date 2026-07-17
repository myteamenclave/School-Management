import api from './axios'

// Academic marks per subject/term. Backend route is /gradebook (NOT /grades —
// that path is grade-LEVELS, see api/grades.ts).

export interface GradeRosterEntry {
  studentId: string
  studentName: string
  studentCode: string
  midtermScore: number | null
  finalScore: number | null
  courseworkScore: number | null
  termScore: number | null
  letterGrade: string | null
  notes: string | null
}

export interface SubjectGradeRoster {
  sectionId: string
  sectionName: string
  subjectId: string
  subjectName: string
  semesterId: string
  semesterName: string
  entries: GradeRosterEntry[]
}

export interface GradeEntryRequest {
  studentId: string
  midterm: number | null
  final: number | null
  coursework: number | null
  notes?: string | null
}

export interface BulkUpsertGradesRequest {
  sectionId: string
  subjectId: string
  semesterId: string
  entries: GradeEntryRequest[]
}

export interface StudentGrade {
  id: string
  subjectId: string
  subjectName: string
  semesterId: string
  semesterName: string
  midtermScore: number | null
  finalScore: number | null
  courseworkScore: number | null
  termScore: number | null
  letterGrade: string | null
  notes: string | null
}

export interface GradeScaleBand {
  id: string
  letter: string
  minScore: number
  maxScore: number
}

export interface UpsertGradeScaleBandRequest {
  letter: string
  minScore: number
  maxScore: number
}

export const GRADEBOOK_KEYS = {
  subjectRoster: (sectionId: string, subjectId: string, semesterId: string) =>
    ['gradebook', 'subject-roster', sectionId, subjectId, semesterId] as const,
  studentGrades: (studentId: string, academicYearId: string) =>
    ['gradebook', 'student', studentId, academicYearId] as const,
  scale: ['grade-scale'] as const,
}

export const gradebookApi = {
  getSubjectRoster: (sectionId: string, subjectId: string, semesterId: string) =>
    api
      .get<SubjectGradeRoster>('/gradebook/subject-roster', {
        params: { sectionId, subjectId, semesterId },
      })
      .then((r) => r.data),

  bulkUpsert: (request: BulkUpsertGradesRequest) =>
    api.put<{ upserted: number }>('/gradebook/bulk', request).then((r) => r.data),

  getStudentGrades: (studentId: string, academicYearId: string) =>
    api
      .get<StudentGrade[]>('/gradebook/student', {
        params: { studentId, academicYearId },
      })
      .then((r) => r.data),
}

export const gradeScaleApi = {
  getAll: () => api.get<GradeScaleBand[]>('/grade-scale').then((r) => r.data),
  create: (body: UpsertGradeScaleBandRequest) =>
    api.post<GradeScaleBand>('/grade-scale', body).then((r) => r.data),
  update: (id: string, body: UpsertGradeScaleBandRequest) =>
    api.put<GradeScaleBand>(`/grade-scale/${id}`, body).then((r) => r.data),
  remove: (id: string) => api.delete(`/grade-scale/${id}`),
}
