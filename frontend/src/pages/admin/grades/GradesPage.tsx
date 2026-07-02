import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus } from 'lucide-react'
import { Accordion } from '../../../components/ui/accordion'
import { Button } from '../../../components/ui/button'
import { GradeAccordionItem } from './components/GradeAccordionItem'
import { CreateGradeModal } from './components/CreateGradeModal'
import { EditGradeModal } from './components/EditGradeModal'
import { gradesApi, GRADE_KEYS } from '../../../api/grades'
import type { GradeDto } from '../../../api/grades'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function GradesPage() {
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [editingGrade, setEditingGrade] = useState<GradeDto | null>(null)
  const [expandedIds, setExpandedIds] = useState<string[]>([])

  const { data: grades = [], isLoading, isError } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })

  const deleteMutation = useMutation({
    mutationFn: gradesApi.delete,
    onSuccess: () => { invalidate(); toast.success('Grade deleted') },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleCreated = (id: string) => setExpandedIds((prev) => [...prev, id])

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-muted-foreground text-sm">
        Loading grades…
      </div>
    )
  }

  if (isError) {
    return (
      <div className="p-8 text-destructive text-sm">
        Failed to load grades. Please refresh the page.
      </div>
    )
  }

  return (
    <div className="px-8 py-8 max-w-4xl mx-auto">
      {/* Page header */}
      <div className="flex items-start justify-between mb-8">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">Grades & Sections</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Define the grade and section structure for your school.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus size={16} className="mr-2" /> Add Grade
        </Button>
      </div>

      {/* Empty state */}
      {grades.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <p className="text-muted-foreground text-sm mb-4">
            No grades yet. Add your first grade to get started.
          </p>
          <Button variant="outline" onClick={() => setCreateOpen(true)}>
            <Plus size={15} className="mr-2" /> Add Grade
          </Button>
        </div>
      )}

      {/* Accordion list */}
      <Accordion
        type="multiple"
        value={expandedIds}
        onValueChange={setExpandedIds}
        className="flex flex-col gap-2"
      >
        {grades.map((grade) => (
          <GradeAccordionItem
            key={grade.id}
            grade={grade}
            onEdit={() => setEditingGrade(grade)}
            onDelete={() => {
              if (window.confirm(`Delete grade "${grade.name}"? This cannot be undone.`))
                deleteMutation.mutate(grade.id)
            }}
          />
        ))}
      </Accordion>

      <CreateGradeModal open={createOpen} onClose={() => setCreateOpen(false)} onCreated={handleCreated} />
      <EditGradeModal grade={editingGrade} onClose={() => setEditingGrade(null)} />
    </div>
  )
}
