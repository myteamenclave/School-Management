import api from './axios'

export interface ParentChild {
  studentId: string
  studentName: string
  studentCode: string
  enrollmentStatus: string
  currentGradeLabel: string | null
  currentSectionName: string | null
}

export interface ParentAcademicYear {
  id: string
  name: string
  isCurrent: boolean
}

// Same shape the admin/teacher student-grade view uses (spec 15 StudentGradeDto).
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

export const PARENT_KEYS = {
  children: () => ['parent', 'children'] as const,
  academicYears: () => ['parent', 'academic-years'] as const,
  childGrades: (childId: string, academicYearId: string) =>
    ['parent', 'grades', childId, academicYearId] as const,
}

export const parentPortalApi = {
  getChildren: () =>
    api.get<ParentChild[]>('/parent/children').then((r) => r.data),

  getAcademicYears: () =>
    api.get<ParentAcademicYear[]>('/parent/academic-years').then((r) => r.data),

  getChildGrades: (childId: string, academicYearId?: string) =>
    api
      .get<StudentGrade[]>(`/parent/children/${childId}/grades`, {
        params: academicYearId ? { academicYearId } : undefined,
      })
      .then((r) => r.data),
}
