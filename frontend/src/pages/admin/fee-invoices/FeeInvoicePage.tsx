import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { ChevronRight } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import {
  Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow,
} from '../../../components/ui/table'
import { feeInvoicesApi, FEE_INVOICE_KEYS } from '../../../api/feeInvoices'
import type { InvoiceStatus, InstallmentStatus } from '../../../api/feeInvoices'

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

function InstallmentStatusBadge({ status }: { status: InstallmentStatus }) {
  const cls =
    status === 'Paid'
      ? 'bg-green-100 text-green-700'
      : status === 'Overdue'
      ? 'bg-red-100 text-red-700'
      : 'bg-zinc-100 text-zinc-500'
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>
      {status}
    </span>
  )
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function FeeInvoicePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: invoice, isLoading, isError } = useQuery({
    queryKey: FEE_INVOICE_KEYS.detail(id!),
    queryFn: () => feeInvoicesApi.getById(id!),
    enabled: !!id,
  })

  const issueMutation = useMutation({
    mutationFn: () => feeInvoicesApi.issue(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: FEE_INVOICE_KEYS.detail(id!) })
      queryClient.invalidateQueries({ queryKey: ['fee-invoices', 'list'] })
      toast.success('Invoice issued')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const cancelMutation = useMutation({
    mutationFn: () => feeInvoicesApi.cancel(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: FEE_INVOICE_KEYS.detail(id!) })
      queryClient.invalidateQueries({ queryKey: ['fee-invoices', 'list'] })
      toast.success('Invoice cancelled')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleCancel = () => {
    if (!window.confirm(`Cancel invoice ${invoice?.invoiceCode}?`)) return
    cancelMutation.mutate()
  }

  if (isLoading) {
    return <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">Loading…</div>
  }
  if (isError || !invoice) {
    return (
      <div className="p-8 text-destructive text-sm">
        Failed to load invoice.{' '}
        <button className="underline" onClick={() => navigate('/admin/fee-invoices')}>Go back</button>
      </div>
    )
  }

  const totalDiscount = invoice.lineItems.reduce((s, li) => s + li.discountAmount, 0)
  const totalFinal = invoice.lineItems.reduce((s, li) => s + li.finalAmount, 0)
  const totalOriginal = invoice.lineItems.reduce((s, li) => s + li.originalAmount, 0)

  return (
    <div className="px-8 py-8 max-w-5xl mx-auto">
      <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
        <button
          onClick={() => navigate('/admin/fee-invoices')}
          className="hover:text-foreground transition-colors"
        >
          Fee Invoices
        </button>
        <ChevronRight size={14} />
        <span className="text-foreground font-medium font-mono">{invoice.invoiceCode}</span>
      </nav>

      <div className="flex items-start justify-between gap-4 mb-6">
        <div className="flex items-center gap-3">
          <h1 className="font-heading text-2xl font-semibold font-mono">{invoice.invoiceCode}</h1>
          <StatusBadge status={invoice.status} />
        </div>
        <div className="flex items-center gap-2">
          {invoice.status === 'Draft' && (
            <Button
              onClick={() => issueMutation.mutate()}
              disabled={issueMutation.isPending}
            >
              {issueMutation.isPending ? 'Issuing…' : 'Issue Invoice'}
            </Button>
          )}
          {invoice.status !== 'Cancelled' && (
            <Button
              variant="destructive"
              onClick={handleCancel}
              disabled={cancelMutation.isPending}
            >
              {cancelMutation.isPending ? 'Cancelling…' : 'Cancel Invoice'}
            </Button>
          )}
        </div>
      </div>

      <div className="rounded-lg border border-border bg-card px-6 py-4 mb-6 grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
        <div className="flex gap-2">
          <span className="text-muted-foreground">Student</span>
          <span className="font-medium">{invoice.studentName}</span>
          <span className="font-mono text-xs text-muted-foreground self-center">({invoice.studentCode})</span>
        </div>
        <div className="flex gap-2">
          <span className="text-muted-foreground">Template</span>
          <span className="font-medium">{invoice.templateName}</span>
        </div>
        <div className="flex gap-2">
          <span className="text-muted-foreground">Year</span>
          <span>{invoice.academicYearName}</span>
        </div>
        <div className="flex gap-2">
          <span className="text-muted-foreground">Total</span>
          <span className="font-semibold font-mono">{currencyFmt.format(invoice.totalAmount)}</span>
        </div>
      </div>

      {/* Line Items */}
      <div className="mb-6">
        <h2 className="text-base font-semibold mb-3">Line Items</h2>
        <div className="rounded-lg border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead className="text-right">Original</TableHead>
                <TableHead className="text-right">Discount</TableHead>
                <TableHead className="text-right">Final</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {invoice.lineItems.map((li) => (
                <TableRow key={li.id}>
                  <TableCell>{li.name}</TableCell>
                  <TableCell className="text-right font-mono text-sm">{currencyFmt.format(li.originalAmount)}</TableCell>
                  <TableCell className="text-right font-mono text-sm text-muted-foreground">
                    {li.discountAmount > 0 ? `–${currencyFmt.format(li.discountAmount)}` : '—'}
                  </TableCell>
                  <TableCell className="text-right font-mono text-sm font-medium">{currencyFmt.format(li.finalAmount)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell className="font-medium">Total</TableCell>
                <TableCell className="text-right font-mono text-sm font-medium">{currencyFmt.format(totalOriginal)}</TableCell>
                <TableCell className="text-right font-mono text-sm text-muted-foreground">
                  {totalDiscount > 0 ? `–${currencyFmt.format(totalDiscount)}` : '—'}
                </TableCell>
                <TableCell className="text-right font-mono text-sm font-medium">{currencyFmt.format(totalFinal)}</TableCell>
              </TableRow>
            </TableFooter>
          </Table>
        </div>
      </div>

      {/* Installments */}
      <div>
        <h2 className="text-base font-semibold mb-3">Installment Schedule</h2>
        {invoice.installments.length === 0 ? (
          <p className="text-sm text-muted-foreground">No installment schedule.</p>
        ) : (
          <div className="rounded-lg border border-border overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead className="text-right">%</TableHead>
                  <TableHead>Due Date</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {invoice.installments.map((inst) => (
                  <TableRow key={inst.id}>
                    <TableCell>{inst.name}</TableCell>
                    <TableCell className="text-right text-sm text-muted-foreground">{inst.percentage}%</TableCell>
                    <TableCell className="text-sm font-mono">
                      {inst.dueDate ?? <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="text-right font-mono text-sm">{currencyFmt.format(inst.amount)}</TableCell>
                    <TableCell><InstallmentStatusBadge status={inst.status} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>
    </div>
  )
}
