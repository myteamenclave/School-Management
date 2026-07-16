import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { keepPreviousData } from '@tanstack/react-query'
import { Plus, Pencil, ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import { Tabs, TabsList, TabsTrigger } from '../../../components/ui/tabs'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../components/ui/table'
import { CreateFeeTemplateModal } from './components/CreateFeeTemplateModal'
import { feeTemplatesApi, FEE_TEMPLATE_KEYS } from '../../../api/feeTemplates'
import type { ListFeeTemplatesParams } from '../../../api/feeTemplates'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'

type StatusTab = 'Active' | 'Inactive' | 'All'
const STATUS_TABS: StatusTab[] = ['Active', 'Inactive', 'All']

function tabToIsActive(tab: StatusTab): boolean | null {
  if (tab === 'Active') return true
  if (tab === 'Inactive') return false
  return null
}

function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
      isActive
        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
        : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400'
    }`}>
      {isActive ? 'Active' : 'Inactive'}
    </span>
  )
}

const currencyFmt = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
})

export function FeeTemplatesPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState<StatusTab>('Active')
  const [academicYearFilter, setAcademicYearFilter] = useState<string | null>(null)
  const [gradeFilter, setGradeFilter] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)

  const queryParams: ListFeeTemplatesParams = {
    isActive: tabToIsActive(tab),
    academicYearId: academicYearFilter,
    gradeId: gradeFilter,
    page,
    pageSize: 20,
  }

  const { data, isLoading, isError } = useQuery({
    queryKey: FEE_TEMPLATE_KEYS.list(queryParams),
    queryFn: () => feeTemplatesApi.list(queryParams),
    placeholderData: keepPreviousData,
  })

  const { data: academicYears } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
    staleTime: Infinity,
  })

  const { data: grades } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
    staleTime: Infinity,
  })

  const totalPages = data ? Math.ceil(data.totalCount / 20) : 0

  return (
    <div className="px-8 py-8 max-w-6xl mx-auto">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">Fee Templates</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Define reusable fee structures per grade and academic year.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus size={16} className="mr-2" /> New Template
        </Button>
      </div>

      <div className="flex items-center justify-between gap-4 mb-4 flex-wrap">
        <Tabs value={tab} onValueChange={(v) => { setTab(v as StatusTab); setPage(1) }}>
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
              value={academicYearFilter ?? 'all'}
              onValueChange={(v) => { setAcademicYearFilter(v === 'all' ? null : v); setPage(1) }}
            >
              <SelectTrigger className="w-44">
                <SelectValue placeholder="All Years" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Years</SelectItem>
                {academicYears?.map((y) => (
                  <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-sm text-muted-foreground whitespace-nowrap">Grade</span>
            <Select
              value={gradeFilter ?? 'all'}
              onValueChange={(v) => { setGradeFilter(v === 'all' ? null : v); setPage(1) }}
            >
              <SelectTrigger className="w-36">
                <SelectValue placeholder="All Grades" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Grades</SelectItem>
                {grades?.map((g) => (
                  <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
          Loading…
        </div>
      )}
      {isError && (
        <div className="flex items-center justify-center h-48 text-sm text-destructive">
          Failed to load fee templates.
        </div>
      )}
      {!isLoading && !isError && (
        <>
          <div className="rounded-lg border border-border overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Grade</TableHead>
                  <TableHead>Academic Year</TableHead>
                  <TableHead>Total</TableHead>
                  <TableHead>Line Items</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-16" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="h-32 text-center text-muted-foreground text-sm">
                      No fee templates found.
                    </TableCell>
                  </TableRow>
                ) : (
                  data?.items.map((template) => (
                    <TableRow
                      key={template.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => navigate(`/admin/fee-templates/${template.id}`)}
                    >
                      <TableCell className="font-medium">{template.name}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{template.gradeName}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{template.academicYearName}</TableCell>
                      <TableCell className="font-mono text-sm">{currencyFmt.format(template.totalAmount)}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">{template.lineItemCount}</TableCell>
                      <TableCell>
                        <StatusBadge isActive={template.isActive} />
                      </TableCell>
                      <TableCell>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={(e) => {
                            e.stopPropagation()
                            navigate(`/admin/fee-templates/${template.id}?edit=true`)
                          }}
                        >
                          <Pencil size={14} />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-end gap-3 mt-4">
              <Button
                size="sm"
                variant="outline"
                disabled={page === 1}
                onClick={() => setPage((p) => p - 1)}
              >
                <ChevronLeft size={15} className="mr-1" /> Prev
              </Button>
              <span className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <Button
                size="sm"
                variant="outline"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next <ChevronRight size={15} className="ml-1" />
              </Button>
            </div>
          )}
        </>
      )}

      <CreateFeeTemplateModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
      />
    </div>
  )
}
