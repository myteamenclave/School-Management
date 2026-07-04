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
import { CreateStudentModal } from './components/CreateStudentModal'
import { EditStudentModal } from './components/EditStudentModal'
import { studentsApi, STUDENT_KEYS } from '../../../api/students'
import type { ListStudentsParams } from '../../../api/students'

type EnrollmentTab = 'Active' | 'Transferred' | 'Graduated' | 'Dropped'
const ENROLLMENT_TABS: EnrollmentTab[] = ['Active', 'Transferred', 'Graduated', 'Dropped']

const STATUS_STYLES: Record<string, string> = {
  Active:      'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Transferred: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  Graduated:   'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400',
  Dropped:     'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_STYLES[status] ?? 'bg-muted text-muted-foreground'}`}>
      {status}
    </span>
  )
}

export function StudentsPage() {
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<EnrollmentTab>('Active')
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

  const handleTabChange = (value: string) => {
    setTab(value as EnrollmentTab)
    setPage(1)
  }

  const queryParams: ListStudentsParams = {
    status: tab,
    search: debouncedSearch,
    page,
    pageSize: 20,
  }

  const { data, isLoading, isError } = useQuery({
    queryKey: STUDENT_KEYS.list(queryParams),
    queryFn: () => studentsApi.list(queryParams),
    placeholderData: keepPreviousData,
  })

  const totalPages = data ? Math.ceil(data.totalCount / 20) : 0

  return (
    <div className="px-8 py-8 max-w-6xl mx-auto">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">Students</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage student records and enrollment status.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus size={16} className="mr-2" /> Add Student
        </Button>
      </div>

      <div className="flex items-center justify-between gap-4 mb-4">
        <Tabs value={tab} onValueChange={handleTabChange}>
          <TabsList>
            {ENROLLMENT_TABS.map((t) => (
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
          Failed to load students.
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
                  <TableHead>Gender</TableHead>
                  <TableHead>Enrolled</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-16" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="h-32 text-center text-muted-foreground text-sm">
                      No students found.
                    </TableCell>
                  </TableRow>
                ) : (
                  data?.items.map((student) => (
                    <TableRow key={student.id}>
                      <TableCell>
                        <span className="font-mono text-xs text-muted-foreground">
                          {student.studentCode}
                        </span>
                      </TableCell>
                      <TableCell className="font-medium">
                        {student.firstName} {student.lastName}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {student.gender}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {student.enrollmentDate}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={student.enrollmentStatus} />
                      </TableCell>
                      <TableCell>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setEditingId(student.id)}
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

      <CreateStudentModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={() => queryClient.invalidateQueries({ queryKey: ['students'] })}
      />
      <EditStudentModal
        studentId={editingId}
        onClose={() => setEditingId(null)}
        onUpdated={() => queryClient.invalidateQueries({ queryKey: ['students'] })}
      />
    </div>
  )
}
