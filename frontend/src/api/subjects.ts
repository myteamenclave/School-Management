import api from './axios'

export interface SubjectSummaryDto {
  id: string
  name: string
  code: string
  description: string | null
  isActive: boolean
  createdAt: string
}

export interface SubjectDto extends SubjectSummaryDto {
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateSubjectRequest {
  name: string
  code: string
  description?: string
}

export interface UpdateSubjectRequest {
  name: string
  description?: string
  isActive: boolean
}

export interface ListSubjectsParams {
  isActive: boolean | null
  search: string
  page: number
  pageSize: number
}

export const SUBJECT_KEYS = {
  list: (p: ListSubjectsParams) => ['subjects', 'list', p] as const,
  detail: (id: string) => ['subjects', 'detail', id] as const,
}

export const subjectsApi = {
  list: (params: ListSubjectsParams) => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.isActive !== null) q.isActive = params.isActive
    if (params.search) q.search = params.search
    return api.get<PagedResult<SubjectSummaryDto>>('/subjects', { params: q }).then((r) => r.data)
  },

  getById: (id: string) =>
    api.get<SubjectDto>(`/subjects/${id}`).then((r) => r.data),

  create: (body: CreateSubjectRequest) =>
    api.post<SubjectDto>('/subjects', body).then((r) => r.data),

  update: (id: string, body: UpdateSubjectRequest) =>
    api.put<SubjectDto>(`/subjects/${id}`, body).then((r) => r.data),
}
