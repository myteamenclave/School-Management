import api from './axios'

export interface StudentSummaryDto {
  id: string
  studentCode: string
  firstName: string
  lastName: string
  dateOfBirth: string
  gender: string
  enrollmentDate: string
  enrollmentStatus: string
}

export interface StudentDto extends StudentSummaryDto {
  guardianName: string | null
  guardianPhone: string | null
  guardianEmail: string | null
  createdAt: string
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateStudentRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
  gender: string
  enrollmentDate: string
  guardianName?: string
  guardianPhone?: string
  guardianEmail?: string
}

export interface UpdateStudentRequest extends CreateStudentRequest {
  enrollmentStatus: string
}

export interface ListStudentsParams {
  status: string
  search: string
  page: number
  pageSize: number
}

export const STUDENT_KEYS = {
  list: (p: ListStudentsParams) => ['students', 'list', p] as const,
  detail: (id: string) => ['students', 'detail', id] as const,
}

export const studentsApi = {
  list: (params: ListStudentsParams) =>
    api.get<PagedResult<StudentSummaryDto>>('/students', { params }).then((r) => r.data),

  getById: (id: string) =>
    api.get<StudentDto>(`/students/${id}`).then((r) => r.data),

  create: (body: CreateStudentRequest) =>
    api.post<StudentDto>('/students', body).then((r) => r.data),

  update: (id: string, body: UpdateStudentRequest) =>
    api.put<StudentDto>(`/students/${id}`, body).then((r) => r.data),
}
