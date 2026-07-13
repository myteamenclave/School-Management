import api from './axios'

export interface TeacherAssignmentDto {
  id: string
  teacherId: string
  subjectId: string
  subjectName: string
  subjectCode: string
  sectionId: string
  sectionName: string
  gradeId: string
  gradeName: string
  academicYearId: string
  academicYearName: string
  createdAt: string
}

export interface CreateTeacherAssignmentRequest {
  subjectId: string
  sectionId: string
  academicYearId: string
}

export const ASSIGNMENT_KEYS = {
  byTeacherAndYear: (teacherId: string, academicYearId: string) =>
    ['assignments', 'teacher', teacherId, academicYearId] as const,
}

export const teacherAssignmentsApi = {
  getByTeacherAndYear: (teacherId: string, academicYearId: string) =>
    api
      .get<TeacherAssignmentDto[]>(`/teachers/${teacherId}/assignments`, {
        params: { academicYearId },
      })
      .then((r) => r.data),

  assign: (teacherId: string, body: CreateTeacherAssignmentRequest) =>
    api
      .post<TeacherAssignmentDto>(`/teachers/${teacherId}/assignments`, body)
      .then((r) => r.data),

  remove: (teacherId: string, assignmentId: string) =>
    api.delete(`/teachers/${teacherId}/assignments/${assignmentId}`),
}
