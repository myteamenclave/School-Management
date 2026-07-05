import { useState, useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { keepPreviousData } from '@tanstack/react-query'
import { Plus, Search, Pencil, ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Tabs, TabsList, TabsTrigger } from '../../../components/ui/tabs'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../components/ui/table'
import { CreateSubjectModal } from './components/CreateSubjectModal'
import { EditSubjectModal } from './components/EditSubjectModal'
import { subjectsApi, SUBJECT_KEYS } from '../../../api/subjects'
import type { ListSubjectsParams } from '../../../api/subjects'

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

export function SubjectsPage() {
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<StatusTab>('Active')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)

  useEffect(() => {
    const t = setTimeout(() => {
      setDebouncedSearch(search)
      setPage(1)
    }, 300)
    return () => clearTimeout(t)
  }, [search])

  const queryParams: ListSubjectsParams = {
    isActive: tabToIsActive(tab),
    search: debouncedSearch,
    page,
    pageSize: 20,
  }

  const { data, isLoading, isError } = useQuery({
    queryKey: SUBJECT_KEYS.list(queryParams),
    queryFn: () => subjectsApi.list(queryParams),
    placeholderData: keepPreviousData,
  })

  const totalPages = data ? Math.ceil(data.totalCount / 20) : 0

  return (
    <div className="px-8 py-8 max-w-6xl mx-auto">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">Subjects</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage the school-wide subject catalog.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus size={16} className="mr-2" /> Add Subject
        </Button>
      </div>

      <div className="flex items-center justify-between gap-4 mb-4">
        <Tabs value={tab} onValueChange={(v) => { setTab(v as StatusTab); setPage(1) }}>
          <TabsList>
            {STATUS_TABS.map((t) => (
              <TabsTrigger key={t} value={t}>{t}</TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        <div className="relative w-64">
          <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search name or code…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
          Loading…
        </div>
      )}
      {isError && (
        <div className="flex items-center justify-center h-48 text-sm text-destructive">
          Failed to load subjects.
        </div>
      )}
      {!isLoading && !isError && (
        <>
          <div className="rounded-lg border border-border overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-16" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="h-32 text-center text-muted-foreground text-sm">
                      No subjects found.
                    </TableCell>
                  </TableRow>
                ) : (
                  data?.items.map((subject) => (
                    <TableRow key={subject.id}>
                      <TableCell>
                        <span className="font-mono text-xs text-muted-foreground">
                          {subject.code}
                        </span>
                      </TableCell>
                      <TableCell className="font-medium">{subject.name}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {subject.description ?? '—'}
                      </TableCell>
                      <TableCell>
                        <StatusBadge isActive={subject.isActive} />
                      </TableCell>
                      <TableCell>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setEditingId(subject.id)}
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

      <CreateSubjectModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={() => queryClient.invalidateQueries({ queryKey: ['subjects'] })}
      />
      <EditSubjectModal
        subjectId={editingId}
        onClose={() => setEditingId(null)}
        onUpdated={() => queryClient.invalidateQueries({ queryKey: ['subjects'] })}
      />
    </div>
  )
}
