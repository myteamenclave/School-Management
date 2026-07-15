import api from './axios'

export interface StudentFeeAssignmentDto {
  id: string
  studentId: string
  studentName: string
  studentCode: string
  feeTemplateId: string
  templateName: string
  academicYearId: string
  academicYearName: string
}

export interface StudentDiscountAssignmentDto {
  id: string
  studentId: string
  discountRuleId: string
  discountRuleName: string
  ruleType: string
  value: number
  academicYearId: string
}

export interface BroadcastAssignmentResult {
  assigned: number
  skipped: number
}

export interface SetStudentAssignmentRequest {
  feeTemplateId: string
  academicYearId: string
}

export interface AddStudentDiscountRequest {
  discountRuleId: string
  academicYearId: string
}

export const FEE_ASSIGNMENT_KEYS = {
  studentAssignment: (studentId: string, academicYearId: string) =>
    ['fee-assignments', 'student', studentId, academicYearId] as const,
  studentDiscounts: (studentId: string, academicYearId: string) =>
    ['fee-assignments', 'discounts', studentId, academicYearId] as const,
}

export const feeAssignmentsApi = {
  broadcast: (templateId: string): Promise<BroadcastAssignmentResult> =>
    api.post<BroadcastAssignmentResult>('/fee-assignments/broadcast', { templateId }).then((r) => r.data),

  getStudentAssignment: (studentId: string, academicYearId: string): Promise<StudentFeeAssignmentDto | null> =>
    api
      .get<StudentFeeAssignmentDto>('/fee-assignments', { params: { studentId, academicYearId } })
      .then((r) => r.data)
      .catch(() => null),

  setStudentAssignment: (studentId: string, body: SetStudentAssignmentRequest): Promise<StudentFeeAssignmentDto> =>
    api
      .put<StudentFeeAssignmentDto>('/fee-assignments', body, { params: { studentId } })
      .then((r) => r.data),

  removeStudentAssignment: (studentId: string, academicYearId: string): Promise<void> =>
    api.delete('/fee-assignments', { params: { studentId, academicYearId } }).then(() => undefined),

  getStudentDiscounts: (studentId: string, academicYearId: string): Promise<StudentDiscountAssignmentDto[]> =>
    api
      .get<StudentDiscountAssignmentDto[]>('/fee-assignments/discounts', { params: { studentId, academicYearId } })
      .then((r) => r.data),

  addStudentDiscount: (studentId: string, body: AddStudentDiscountRequest): Promise<StudentDiscountAssignmentDto> =>
    api
      .post<StudentDiscountAssignmentDto>('/fee-assignments/discounts', body, { params: { studentId } })
      .then((r) => r.data),

  removeStudentDiscount: (id: string): Promise<void> =>
    api.delete(`/fee-assignments/discounts/${id}`).then(() => undefined),
}
