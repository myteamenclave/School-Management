import { Link } from 'react-router-dom'
import { Wallet, AlertTriangle, TrendingUp, FileWarning } from 'lucide-react'
import type { FinanceSummaryDto } from '../../../api/dashboard'
import { formatMoney, formatPercent } from './format'

interface FinanceTilesProps {
  finance: FinanceSummaryDto
  academicYearId: string
}

function Kpi({
  label,
  value,
  icon,
  tone = 'default',
}: {
  label: string
  value: string
  icon: React.ReactNode
  tone?: 'default' | 'warning' | 'good'
}) {
  const toneClass =
    tone === 'warning' ? 'text-destructive' : tone === 'good' ? 'text-accent' : 'text-foreground'
  return (
    <div className="rounded-lg border border-border bg-card p-5">
      <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
        {icon}
        {label}
      </div>
      <p className={`mt-2 font-heading text-2xl font-semibold ${toneClass}`}>{value}</p>
    </div>
  )
}

export function FinanceTiles({ finance, academicYearId }: FinanceTilesProps) {
  const invoicesHref = `/admin/fee-invoices?academicYearId=${academicYearId}`
  return (
    <div className="space-y-3">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Kpi
          label="Collected"
          value={formatMoney(finance.collected)}
          icon={<Wallet size={14} />}
          tone="good"
        />
        <Kpi label="Outstanding" value={formatMoney(finance.outstanding)} icon={<Wallet size={14} />} />
        <Kpi
          label="Overdue"
          value={formatMoney(finance.overdue)}
          icon={<AlertTriangle size={14} />}
          tone={finance.overdue > 0 ? 'warning' : 'default'}
        />
        <Kpi
          label="Collection rate"
          value={formatPercent(finance.collectionRate)}
          icon={<TrendingUp size={14} />}
        />
      </div>

      {finance.draftInvoiceCount > 0 && (
        <Link
          to={invoicesHref}
          className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm text-amber-800 hover:bg-amber-100"
        >
          <FileWarning size={15} />
          <span>
            <strong>{finance.draftInvoiceCount}</strong> draft invoice
            {finance.draftInvoiceCount === 1 ? '' : 's'} not yet issued — billed value not counted until issued.
          </span>
        </Link>
      )}
    </div>
  )
}
