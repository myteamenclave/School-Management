import api from './axios'

export interface ParentAccountDto {
  parentUserId: string
  email: string
  displayName: string
  accountCreatedAt: string // ISO timestamp
}

export interface ParentLoginResultDto {
  parentUserId: string
  email: string
  displayName: string
  accountCreated: boolean // false = an existing Parent account was reused (temp password NOT applied)
  linkCreated: boolean // false = the link already existed (no-op)
}

export interface CreateParentLoginRequest {
  temporaryPassword: string
}

export const PARENT_ACCOUNT_KEYS = {
  forStudent: (studentId: string) => ['parent-accounts', studentId] as const,
}

export const parentAccountsApi = {
  list: (studentId: string) =>
    api.get<ParentAccountDto[]>(`/students/${studentId}/parents`).then((r) => r.data),

  createLogin: (studentId: string, body: CreateParentLoginRequest) =>
    api
      .post<ParentLoginResultDto>(`/students/${studentId}/parent-login`, body)
      .then((r) => r.data),

  removeLink: (studentId: string, parentUserId: string) =>
    api.delete(`/students/${studentId}/parents/${parentUserId}`),
}
