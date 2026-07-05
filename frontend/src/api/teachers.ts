import api from './axios'

export interface TeacherSummaryDto {
  id: string
  teacherCode: string
  firstName: string
  lastName: string
  phone: string | null
  joiningDate: string
  isActive: boolean
  email: string
}

export interface TeacherDto extends TeacherSummaryDto {
  userId: string
  createdAt: string
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateTeacherRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone?: string
  joiningDate: string
}

export interface UpdateTeacherRequest {
  firstName: string
  lastName: string
  phone?: string
  joiningDate: string
  isActive: boolean
}

export interface ListTeachersParams {
  isActive: boolean | null
  search: string
  page: number
  pageSize: number
}

export const TEACHER_KEYS = {
  list: (p: ListTeachersParams) => ['teachers', 'list', p] as const,
  detail: (id: string) => ['teachers', 'detail', id] as const,
}

export const teachersApi = {
  list: (params: ListTeachersParams) => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.isActive !== null) q.isActive = params.isActive
    if (params.search) q.search = params.search
    return api.get<PagedResult<TeacherSummaryDto>>('/teachers', { params: q }).then((r) => r.data)
  },

  getById: (id: string) =>
    api.get<TeacherDto>(`/teachers/${id}`).then((r) => r.data),

  create: (body: CreateTeacherRequest) =>
    api.post<TeacherDto>('/teachers', body).then((r) => r.data),

  update: (id: string, body: UpdateTeacherRequest) =>
    api.put<TeacherDto>(`/teachers/${id}`, body).then((r) => r.data),
}
