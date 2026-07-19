import { Link } from 'react-router-dom'
import { ArrowUpRight } from 'lucide-react'
import type { ReactNode } from 'react'

interface DashboardCardProps {
  title: string
  description?: string
  /** Drill-through target — renders a link in the header when provided. */
  to?: string
  linkLabel?: string
  children: ReactNode
  className?: string
}

export function DashboardCard({ title, description, to, linkLabel, children, className }: DashboardCardProps) {
  return (
    <section className={`rounded-lg border border-border bg-card p-5 ${className ?? ''}`}>
      <div className="flex items-start justify-between gap-4 mb-4">
        <div>
          <h2 className="font-heading text-base font-semibold text-foreground">{title}</h2>
          {description && <p className="text-xs text-muted-foreground mt-0.5">{description}</p>}
        </div>
        {to && (
          <Link
            to={to}
            className="inline-flex items-center gap-1 text-xs font-medium text-secondary hover:underline whitespace-nowrap"
          >
            {linkLabel ?? 'View'} <ArrowUpRight size={13} />
          </Link>
        )}
      </div>
      {children}
    </section>
  )
}

// Muted placeholder shown when a tile has no data for the selected year.
export function EmptyTile({ message }: { message: string }) {
  return (
    <div className="flex items-center justify-center h-40 text-sm text-muted-foreground">
      {message}
    </div>
  )
}
