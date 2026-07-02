import api from './axios'

export interface SemesterDto {
  id: string
  academicYearId: string
  name: string
  startDate: string
  endDate: string
  isCurrent: boolean
}

export interface AcademicYearDto {
  id: string
  name: string
  startDate: string
  endDate: string
  status: 'Active' | 'Archived'
  isCurrent: boolean
  semesters: SemesterDto[]
}

export interface CreateAcademicYearRequest {
  name: string
  startDate: string
  endDate: string
}

export interface UpdateSemesterRequest {
  name: string
  startDate: string
  endDate: string
}

export const ACADEMIC_YEAR_KEYS = {
  all: ['academic-years'] as const,
}

export const academicYearsApi = {
  list: () =>
    api.get<AcademicYearDto[]>('/academic-years').then((r) => r.data),

  create: (body: CreateAcademicYearRequest) =>
    api.post<AcademicYearDto>('/academic-years', body).then((r) => r.data),

  updateSemester: (yearId: string, semesterId: string, body: UpdateSemesterRequest) =>
    api
      .put<SemesterDto>(`/academic-years/${yearId}/semesters/${semesterId}`, body)
      .then((r) => r.data),

  setCurrentYear: (id: string) =>
    api.post(`/academic-years/${id}/set-current`).then((r) => r.data),

  setCurrentSemester: (yearId: string, semesterId: string) =>
    api
      .post(`/academic-years/${yearId}/semesters/${semesterId}/set-current`)
      .then((r) => r.data),

  archive: (id: string) =>
    api.post(`/academic-years/${id}/archive`).then((r) => r.data),
}
