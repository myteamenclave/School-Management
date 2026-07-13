import api from './axios'

export interface EnrollmentDto {
  id: string
  studentId: string
  studentCode: string
  studentFirstName: string
  studentLastName: string
  sectionId: string
  sectionName: string
  gradeId: string
  gradeName: string
  academicYearId: string
  academicYearName: string
  createdAt: string
  updatedAt: string | null
}

export interface CreateEnrollmentRequest {
  studentId: string
  academicYearId: string
}

export interface TransferEnrollmentRequest {
  sectionId: string
}

export const ENROLLMENT_KEYS = {
  bySectionAndYear: (sectionId: string, academicYearId: string) =>
    ['enrollments', 'section', sectionId, academicYearId] as const,
  enrolledIds: (academicYearId: string) =>
    ['enrollments', 'enrolled-ids', academicYearId] as const,
  byStudent: (studentId: string) =>
    ['enrollments', 'student', studentId] as const,
}

export const enrollmentsApi = {
  getBySectionAndYear: (sectionId: string, academicYearId: string) =>
    api
      .get<EnrollmentDto[]>(`/sections/${sectionId}/enrollments`, {
        params: { academicYearId },
      })
      .then((r) => r.data),

  getEnrolledIds: (academicYearId: string) =>
    api
      .get<string[]>('/enrollments/enrolled-ids', { params: { academicYearId } })
      .then((r) => r.data),

  getByStudentId: (studentId: string) =>
    api
      .get<EnrollmentDto[]>('/enrollments', { params: { studentId } })
      .then((r) => r.data),

  enroll: (sectionId: string, body: CreateEnrollmentRequest) =>
    api
      .post<EnrollmentDto>(`/sections/${sectionId}/enrollments`, body)
      .then((r) => r.data),

  transfer: (id: string, body: TransferEnrollmentRequest) =>
    api.put<EnrollmentDto>(`/enrollments/${id}`, body).then((r) => r.data),

  remove: (id: string) => api.delete(`/enrollments/${id}`),
}
