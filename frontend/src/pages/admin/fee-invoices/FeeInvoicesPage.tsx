import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { keepPreviousData } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Eye, CheckCircle, XCircle, ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import { Tabs, TabsList, TabsTrigger } from '../../../components/ui/tabs'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '../../../components/ui/select'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '../../../components/ui/table'
import { feeInvoicesApi, FEE_INVOICE_KEYS } from '../../../api/feeInvoices'
import type { InvoiceStatus, ListFeeInvoicesParams } from '../../../api/feeInvoices'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'

type StatusTab = 'All' | InvoiceStatus
const STATUS_TABS: StatusTab[] = ['All', 'Draft', 'Issued', 'Cancelled']

const currencyFmt = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
})

function StatusBadge({ status }: { status: InvoiceStatus }) {
  const cls =
    status === 'Issued'
      ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
      : status === 'Cancelled'
      ? 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
      : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400'
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${cls}`}>
      {status}
    </span>
  )
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function FeeInvoicesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()

  const [tab, setTab] = useState<StatusTab>('All')
  const [gradeFilter, setGradeFilter] = useState<string | null>(
    searchParams.get('gradeId')
  )
  const [yearFilter, setYearFilter] = useState<string | null>(
    searchParams.get('academicYearId')
  )
  const [page, setPage] = useState(1)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())

  const queryParams: ListFeeInvoicesParams = {
    status: tab === 'All' ? null : tab,
    gradeId: gradeFilter,
    academicYearId: yearFilter,
    page,
    pageSize: 20,
  }

  const { data, isLoading, isError } = useQuery({
    queryKey: FEE_INVOICE_KEYS.list(queryParams),
    queryFn: () => feeInvoicesApi.list(queryParams),
    placeholderData: keepPreviousData,
  })

  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
    staleTime: Infinity,
  })

  const { data: grades = [] } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
    staleTime: Infinity,
  })

  const issueMutation = useMutation({
    mutationFn: (id: string) => feeInvoicesApi.issue(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['fee-invoices', 'list'] })
      toast.success('Invoice issued')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const cancelMutation = useMutation({
    mutationFn: (id: string) => feeInvoicesApi.cancel(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['fee-invoices', 'list'] })
      toast.success('Invoice cancelled')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const bulkIssueMutation = useMutation({
    mutationFn: (ids: string[]) => feeInvoicesApi.bulkIssue(ids),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['fee-invoices', 'list'] })
      setSelectedIds(new Set())
      toast.success(
        `Issued ${result.issued} invoice${result.issued !== 1 ? 's' : ''}. ` +
        (result.skipped > 0 ? `${result.skipped} skipped.` : '')
      )
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleCancel = (id: string, code: string) => {
    if (!window.confirm(`Cancel invoice ${code}?`)) return
    cancelMutation.mutate(id)
  }

  const toggleSelect = (id: string, isDraft: boolean) => {
    if (!isDraft) return
    setSelectedIds((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  const totalPages = data ? Math.ceil(data.totalCount / 20) : 0

  return (
    <div className="px-8 py-8 max-w-7xl mx-auto">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">Fee Invoices</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage student fee invoices across all grades and academic years.
          </p>
        </div>
        {selectedIds.size > 0 && (
          <Button
            onClick={() => bulkIssueMutation.mutate([...selectedIds])}
            disabled={bulkIssueMutation.isPending}
          >
            <CheckCircle size={15} className="mr-1.5" />
            Issue Selected ({selectedIds.size})
          </Button>
        )}
      </div>

      <div className="flex items-center justify-between gap-4 mb-4 flex-wrap">
        <Tabs
          value={tab}
          onValueChange={(v) => {
            setTab(v as StatusTab)
            setPage(1)
            setSelectedIds(new Set())
          }}
        >
          <TabsList>
            {STATUS_TABS.map((t) => (
              <TabsTrigger key={t} value={t}>{t}</TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5">
            <span className="text-sm text-muted-foreground whitespace-nowrap">Year</span>
            <Select
              value={yearFilter ?? 'all'}
              onValueChange={(v) => {
                const val = v === 'all' ? null : v
                setYearFilter(val)
                setPage(1)
                setSearchParams(val ? { academicYearId: val, ...(gradeFilter ? { gradeId: gradeFilter } : {}) } : {})
              }}
            >
              <SelectTrigger className="w-44">
                <SelectValue placeholder="All Years" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Years</SelectItem>
                {years.map((y) => (
                  <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-sm text-muted-foreground whitespace-nowrap">Grade</span>
            <Select
              value={gradeFilter ?? 'all'}
              onValueChange={(v) => {
                const val = v === 'all' ? null : v
                setGradeFilter(val)
                setPage(1)
              }}
            >
              <SelectTrigger className="w-36">
                <SelectValue placeholder="All Grades" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Grades</SelectItem>
                {grades.map((g) => (
                  <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">Loading…</div>
      )}
      {isError && (
        <div className="flex items-center justify-center h-48 text-sm text-destructive">Failed to load invoices.</div>
      )}
      {!isLoading && !isError && (
        <>
          <div className="rounded-lg border border-border overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-8" />
                  <TableHead>Code</TableHead>
                  <TableHead>Student</TableHead>
                  <TableHead>Template</TableHead>
                  <TableHead>Year</TableHead>
                  <TableHead>Total</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8} className="h-32 text-center text-muted-foreground text-sm">
                      No invoices found.
                    </TableCell>
                  </TableRow>
                ) : (
                  data?.items.map((inv) => (
                    <TableRow
                      key={inv.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => navigate(`/admin/fee-invoices/${inv.id}`)}
                    >
                      <TableCell onClick={(e) => e.stopPropagation()}>
                        <input
                          type="checkbox"
                          checked={selectedIds.has(inv.id)}
                          disabled={inv.status !== 'Draft'}
                          onChange={() => toggleSelect(inv.id, inv.status === 'Draft')}
                          className="h-4 w-4 rounded border-border accent-primary cursor-pointer disabled:opacity-30"
                        />
                      </TableCell>
                      <TableCell className="font-mono text-sm">{inv.invoiceCode}</TableCell>
                      <TableCell>
                        <div className="font-medium text-sm">{inv.studentName}</div>
                        <div className="text-xs text-muted-foreground font-mono">{inv.studentCode}</div>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">{inv.templateName}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{inv.academicYearName}</TableCell>
                      <TableCell className="font-mono text-sm">{currencyFmt.format(inv.totalAmount)}</TableCell>
                      <TableCell><StatusBadge status={inv.status} /></TableCell>
                      <TableCell onClick={(e) => e.stopPropagation()}>
                        <div className="flex items-center gap-1">
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 w-7 p-0"
                            title="View details"
                            onClick={() => navigate(`/admin/fee-invoices/${inv.id}`)}
                          >
                            <Eye size={13} />
                          </Button>
                          {inv.status === 'Draft' && (
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 w-7 p-0 text-green-600 hover:text-green-600"
                              title="Issue invoice"
                              disabled={issueMutation.isPending}
                              onClick={() => issueMutation.mutate(inv.id)}
                            >
                              <CheckCircle size={13} />
                            </Button>
                          )}
                          {inv.status !== 'Cancelled' && (
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 w-7 p-0 text-destructive hover:text-destructive"
                              title="Cancel invoice"
                              disabled={cancelMutation.isPending}
                              onClick={() => handleCancel(inv.id, inv.invoiceCode)}
                            >
                              <XCircle size={13} />
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-end gap-3 mt-4">
              <Button size="sm" variant="outline" disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
                <ChevronLeft size={15} className="mr-1" /> Prev
              </Button>
              <span className="text-sm text-muted-foreground">Page {page} of {totalPages}</span>
              <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next <ChevronRight size={15} className="ml-1" />
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
