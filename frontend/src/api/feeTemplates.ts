import api from './axios'

export type DiscountRuleType = 'Percentage' | 'FlatAmount'

export interface FeeLineItemDto {
  id: string
  name: string
  amount: number
  displayOrder: number
}

export interface FeeInstallmentDto {
  id: string
  name: string
  percentage: number
  dueLabel: string | null
  displayOrder: number
}

export interface DiscountRuleDto {
  id: string
  name: string
  ruleType: DiscountRuleType
  value: number
  feeLineItemId: string | null
  feeLineItemName: string | null
}

export interface FeeTemplateSummaryDto {
  id: string
  name: string
  academicYearId: string
  academicYearName: string
  gradeId: string
  gradeName: string
  totalAmount: number
  lineItemCount: number
  isActive: boolean
  createdAt: string
}

export interface FeeTemplateDto {
  id: string
  name: string
  academicYearId: string
  academicYearName: string
  gradeId: string
  gradeName: string
  totalAmount: number
  isActive: boolean
  isFrozen: boolean
  createdAt: string
  updatedAt: string | null
  lineItems: FeeLineItemDto[]
  installments: FeeInstallmentDto[]
  discountRules: DiscountRuleDto[]
}

export interface CreateFeeTemplateRequest {
  name: string
  academicYearId: string
  gradeId: string
}

export interface UpdateFeeTemplateRequest {
  name: string
  isActive: boolean
}

export interface LineItemInput {
  id?: string
  name: string
  amount: number
  displayOrder: number
}

export interface InstallmentInput {
  name: string
  percentage: number
  dueLabel: string
  displayOrder: number
}

export interface DiscountRuleInput {
  name: string
  ruleType: DiscountRuleType
  value: number
  feeLineItemId?: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ListFeeTemplatesParams {
  isActive: boolean | null
  academicYearId: string | null
  gradeId: string | null
  page: number
  pageSize: number
}

export const FEE_TEMPLATE_KEYS = {
  list: (p: ListFeeTemplatesParams) => ['fee-templates', 'list', p] as const,
  detail: (id: string) => ['fee-templates', 'detail', id] as const,
}

export const feeTemplatesApi = {
  list: (params: ListFeeTemplatesParams) => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.isActive !== null) q.isActive = params.isActive
    if (params.academicYearId) q.academicYearId = params.academicYearId
    if (params.gradeId) q.gradeId = params.gradeId
    return api
      .get<PagedResult<FeeTemplateSummaryDto>>('/fee-templates', { params: q })
      .then((r) => r.data)
  },

  getById: (id: string) =>
    api.get<FeeTemplateDto>(`/fee-templates/${id}`).then((r) => r.data),

  create: (body: CreateFeeTemplateRequest) =>
    api.post<FeeTemplateDto>('/fee-templates', body).then((r) => r.data),

  updateHeader: (id: string, body: UpdateFeeTemplateRequest) =>
    api.put<FeeTemplateDto>(`/fee-templates/${id}`, body).then((r) => r.data),

  replaceLineItems: (id: string, items: LineItemInput[]) =>
    api
      .put<FeeTemplateDto>(`/fee-templates/${id}/line-items`, { items })
      .then((r) => r.data),

  replaceInstallments: (id: string, items: InstallmentInput[]) =>
    api
      .put<FeeTemplateDto>(`/fee-templates/${id}/installments`, { items })
      .then((r) => r.data),

  replaceDiscountRules: (id: string, items: DiscountRuleInput[]) =>
    api
      .put<FeeTemplateDto>(`/fee-templates/${id}/discount-rules`, { items })
      .then((r) => r.data),
}

