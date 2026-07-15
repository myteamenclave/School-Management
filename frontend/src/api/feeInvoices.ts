import api from './axios'

export type InvoiceStatus = 'Draft' | 'Issued' | 'Cancelled'
export type InstallmentStatus = 'Pending' | 'Paid' | 'Overdue'

export interface FeeInvoiceSummaryDto {
  id: string
  invoiceCode: string
  studentId: string
  studentName: string
  studentCode: string
  academicYearId: string
  academicYearName: string
  feeTemplateId: string
  templateName: string
  totalAmount: number
  status: InvoiceStatus
  issuedAt: string | null
  createdAt: string
}

export interface FeeInvoiceLineItemDto {
  id: string
  name: string
  originalAmount: number
  discountAmount: number
  finalAmount: number
  displayOrder: number
}

export interface FeeInvoiceInstallmentDto {
  id: string
  name: string
  percentage: number
  dueDate: string | null
  amount: number
  status: InstallmentStatus
  displayOrder: number
}

export interface FeeInvoiceDto {
  id: string
  invoiceCode: string
  studentId: string
  studentName: string
  studentCode: string
  academicYearId: string
  academicYearName: string
  feeTemplateId: string
  templateName: string
  totalAmount: number
  status: InvoiceStatus
  issuedAt: string | null
  cancelledAt: string | null
  createdAt: string
  updatedAt: string | null
  lineItems: FeeInvoiceLineItemDto[]
  installments: FeeInvoiceInstallmentDto[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ListFeeInvoicesParams {
  status: InvoiceStatus | null
  gradeId: string | null
  academicYearId: string | null
  page: number
  pageSize: number
}

export interface InstallmentDueDateInput {
  templateInstallmentId: string
  dueDate: string
}

export interface GenerateInvoicesRequest {
  gradeId: string
  academicYearId: string
  installmentDueDates: InstallmentDueDateInput[]
}

export interface GenerateInvoicesResult {
  generated: number
  skipped: number
}

export interface BulkIssueResult {
  issued: number
  skipped: number
}

export const FEE_INVOICE_KEYS = {
  list: (p: ListFeeInvoicesParams) => ['fee-invoices', 'list', p] as const,
  detail: (id: string) => ['fee-invoices', 'detail', id] as const,
}

export const feeInvoicesApi = {
  list: (params: ListFeeInvoicesParams): Promise<PagedResult<FeeInvoiceSummaryDto>> => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.status) q.status = params.status
    if (params.gradeId) q.gradeId = params.gradeId
    if (params.academicYearId) q.academicYearId = params.academicYearId
    return api.get<PagedResult<FeeInvoiceSummaryDto>>('/fee-invoices', { params: q }).then((r) => r.data)
  },

  getById: (id: string): Promise<FeeInvoiceDto> =>
    api.get<FeeInvoiceDto>(`/fee-invoices/${id}`).then((r) => r.data),

  generate: (body: GenerateInvoicesRequest): Promise<GenerateInvoicesResult> =>
    api.post<GenerateInvoicesResult>('/fee-invoices/generate', body).then((r) => r.data),

  issue: (id: string): Promise<FeeInvoiceDto> =>
    api.post<FeeInvoiceDto>(`/fee-invoices/${id}/issue`).then((r) => r.data),

  cancel: (id: string): Promise<FeeInvoiceDto> =>
    api.post<FeeInvoiceDto>(`/fee-invoices/${id}/cancel`).then((r) => r.data),

  bulkIssue: (ids: string[]): Promise<BulkIssueResult> =>
    api.post<BulkIssueResult>('/fee-invoices/bulk-issue', { ids }).then((r) => r.data),
}
