import api from './axios'

export interface SectionDto {
  id: string
  gradeId: string
  name: string
}

export interface GradeDto {
  id: string
  name: string
  displayOrder: number
  sections: SectionDto[]
}

export interface CreateGradeRequest {
  name: string
  displayOrder: number
}

export interface UpdateGradeRequest {
  name: string
  displayOrder: number
}

export interface CreateSectionRequest {
  name: string
}

export interface UpdateSectionRequest {
  name: string
}

export const GRADE_KEYS = {
  all: ['grades'] as const,
}

export const gradesApi = {
  list: () =>
    api.get<GradeDto[]>('/grades').then((r) => r.data),

  create: (body: CreateGradeRequest) =>
    api.post<GradeDto>('/grades', body).then((r) => r.data),

  update: (id: string, body: UpdateGradeRequest) =>
    api.put<GradeDto>(`/grades/${id}`, body).then((r) => r.data),

  delete: (id: string) =>
    api.delete(`/grades/${id}`),

  addSection: (gradeId: string, body: CreateSectionRequest) =>
    api.post<SectionDto>(`/grades/${gradeId}/sections`, body).then((r) => r.data),

  updateSection: (gradeId: string, sectionId: string, body: UpdateSectionRequest) =>
    api.put<SectionDto>(`/grades/${gradeId}/sections/${sectionId}`, body).then((r) => r.data),

  deleteSection: (gradeId: string, sectionId: string) =>
    api.delete(`/grades/${gradeId}/sections/${sectionId}`),
}
