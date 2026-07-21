import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Receipt, AlertTriangle } from 'lucide-react'
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../components/ui/table'
import { parentPortalApi, PARENT_KEYS } from '../../../api/parentPortal'
import { useParentChildYear } from '../useParentChildYear'
import { ParentChildYearBar } from '../ParentChildYearBar'

// Matches the admin fee pages (spec 13) — one currency convention across the app.
const currencyFmt = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
})

const EmptyState = ({ children }: { children: React.ReactNode }) => (
  <div className="rounded-lg border border-dashed border-border p-10 text-center text-sm text-muted-foreground">
    {children}
  </div>
)

function StatCard({ label, value, className }: { label: string; value: string; className?: string }) {
  return (
    <div className="rounded-lg border border-border p-4 text-center">
      <div className={`text-2xl font-semibold tabular-nums ${className ?? 'text-foreground'}`}>{value}</div>
      <div className="mt-1 text-xs text-muted-foreground">{label}</div>
    </div>
  )
}

function formatDate(iso: string) {
  const d = new Date(iso + 'T00:00:00')
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export function ChildFeesPage() {
  const {
    children,
    years,
    childId,
    setChildId,
    academicYearId,
    setAcademicYearId,
    childrenLoading,
    childrenError,
    selectedChild,
    selectedYear,
  } = useParentChildYear()

  const enabled = !!(childId && academicYearId)

  const { data, isFetching } = useQuery({
    queryKey: enabled
      ? PARENT_KEYS.childFees(childId!, academicYearId!)
      : ['parent', 'fees', 'disabled'],
    queryFn: () => parentPortalApi.getChildFees(childId!, academicYearId!),
    enabled,
  })

  const summary = data?.summary
  const invoice = data?.invoice ?? null

  // Overdue is a server-owned set of ids — the UI highlights, never recomputes the rule.
  const overdueIds = useMemo(
    () => new Set(summary?.overdueInstallmentIds ?? []),
    [summary],
  )

  const installments = useMemo(
    () => [...(invoice?.installments ?? [])].sort((a, b) => a.displayOrder - b.displayOrder),
    [invoice],
  )
  const lineItems = useMemo(
    () => [...(invoice?.lineItems ?? [])].sort((a, b) => a.displayOrder - b.displayOrder),
    [invoice],
  )

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <Receipt size={22} className="text-primary" />
        <div>
          <h1 className="text-xl font-semibold text-foreground">Fees</h1>
          <p className="text-sm text-muted-foreground">Fee balance and invoice by academic year</p>
        </div>
      </div>

      {childrenLoading ? (
        <EmptyState>Loading…</EmptyState>
      ) : childrenError ? (
        <EmptyState>Something went wrong loading your children. Please refresh to try again.</EmptyState>
      ) : children.length === 0 ? (
        <EmptyState>
          No students are linked to your account yet. Please contact the school office.
        </EmptyState>
      ) : (
        <>
          <ParentChildYearBar
            children={children}
            years={years}
            childId={childId}
            academicYearId={academicYearId}
            onChildChange={setChildId}
            onYearChange={setAcademicYearId}
          />

          {isFetching ? (
            <EmptyState>Loading fees…</EmptyState>
          ) : !summary || !summary.hasInvoice || !invoice ? (
            <EmptyState>
              No fees have been issued for {selectedChild?.studentName ?? 'this student'}
              {selectedYear ? ` in ${selectedYear.name}` : ''} yet.
            </EmptyState>
          ) : (
            <>
              {/* Balance hero. */}
              <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
                <StatCard
                  label="Outstanding"
                  value={currencyFmt.format(summary.outstanding)}
                  className={summary.outstanding > 0 ? 'text-primary' : 'text-green-600 dark:text-green-400'}
                />
                <StatCard label="Billed" value={currencyFmt.format(summary.totalBilled)} />
                <StatCard label="Paid" value={currencyFmt.format(summary.totalPaid)} />
                <StatCard
                  label={summary.nextDueDate ? `Next Due · ${formatDate(summary.nextDueDate)}` : 'Next Due'}
                  value={summary.nextDueAmount === null ? '—' : currencyFmt.format(summary.nextDueAmount)}
                />
              </div>

              {/* Overdue callout. */}
              {summary.overdueCount > 0 && (
                <div className="mb-6 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/40 dark:bg-red-900/20 dark:text-red-400">
                  <AlertTriangle size={16} className="shrink-0" />
                  <span>
                    {summary.overdueCount} installment{summary.overdueCount > 1 ? 's' : ''} past due ·{' '}
                    {currencyFmt.format(summary.overdueAmount)} overdue
                  </span>
                </div>
              )}

              {/* Installment schedule. */}
              <div className="mb-6">
                <div className="mb-2 flex items-center justify-between">
                  <h2 className="text-sm font-semibold text-foreground">Installment Schedule</h2>
                  <span className="font-mono text-xs text-muted-foreground">{invoice.invoiceCode}</span>
                </div>
                <div className="rounded-lg border border-border overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Installment</TableHead>
                        <TableHead className="w-40">Due Date</TableHead>
                        <TableHead className="w-32 text-right">Amount</TableHead>
                        <TableHead className="w-28">Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {installments.map((inst) => {
                        const isOverdue = overdueIds.has(inst.id)
                        return (
                          <TableRow key={inst.id}>
                            <TableCell className="text-sm">{inst.name}</TableCell>
                            <TableCell className="text-sm tabular-nums">
                              {inst.dueDate ? formatDate(inst.dueDate) : '—'}
                            </TableCell>
                            <TableCell className="text-sm text-right tabular-nums">
                              {currencyFmt.format(inst.amount)}
                            </TableCell>
                            <TableCell>
                              {isOverdue ? (
                                <span className="inline-flex items-center rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-medium text-red-700 dark:bg-red-900/30 dark:text-red-400">
                                  Overdue
                                </span>
                              ) : inst.status === 'Paid' ? (
                                <span className="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/30 dark:text-green-400">
                                  Paid
                                </span>
                              ) : (
                                <span className="inline-flex items-center rounded-full bg-zinc-100 px-2.5 py-0.5 text-xs font-medium text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
                                  Pending
                                </span>
                              )}
                            </TableCell>
                          </TableRow>
                        )
                      })}
                    </TableBody>
                  </Table>
                </div>
              </div>

              {/* Line-item breakdown. */}
              <div>
                <h2 className="mb-2 text-sm font-semibold text-foreground">Breakdown</h2>
                <div className="rounded-lg border border-border overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Item</TableHead>
                        <TableHead className="w-32 text-right">Original</TableHead>
                        <TableHead className="w-32 text-right">Discount</TableHead>
                        <TableHead className="w-32 text-right">Final</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {lineItems.map((li) => (
                        <TableRow key={li.id}>
                          <TableCell className="text-sm">{li.name}</TableCell>
                          <TableCell className="text-sm text-right tabular-nums">
                            {currencyFmt.format(li.originalAmount)}
                          </TableCell>
                          <TableCell className="text-sm text-right tabular-nums text-muted-foreground">
                            {li.discountAmount > 0 ? `−${currencyFmt.format(li.discountAmount)}` : '—'}
                          </TableCell>
                          <TableCell className="text-sm text-right tabular-nums">
                            {currencyFmt.format(li.finalAmount)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                    <TableFooter>
                      <TableRow>
                        <TableCell className="text-sm font-semibold" colSpan={3}>
                          Total
                        </TableCell>
                        <TableCell className="text-sm text-right font-semibold tabular-nums">
                          {currencyFmt.format(invoice.totalAmount)}
                        </TableCell>
                      </TableRow>
                    </TableFooter>
                  </Table>
                </div>
              </div>
            </>
          )}
        </>
      )}
    </div>
  )
}
