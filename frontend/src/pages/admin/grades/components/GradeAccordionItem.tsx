import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Check, X, Plus, Pencil, Trash2 } from 'lucide-react'
import {
  AccordionItem,
  AccordionTrigger,
  AccordionContent,
} from '../../../../components/ui/accordion'
import { Badge } from '../../../../components/ui/badge'
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
  TooltipProvider,
} from '../../../../components/ui/tooltip'
import { Button } from '../../../../components/ui/button'
import { SectionChip } from './SectionChip'
import type { GradeDto } from '../../../../api/grades'
import { gradesApi, GRADE_KEYS } from '../../../../api/grades'

interface GradeAccordionItemProps {
  grade: GradeDto
  onEdit: () => void
  onDelete: () => void
}

export function GradeAccordionItem({ grade, onEdit, onDelete }: GradeAccordionItemProps) {
  const queryClient = useQueryClient()
  const [addingSectionOpen, setAddingSectionOpen] = useState(false)
  const [newSectionName, setNewSectionName] = useState('')

  const addSectionMutation = useMutation({
    mutationFn: (name: string) => gradesApi.addSection(grade.id, { name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })
      setNewSectionName('')
      setAddingSectionOpen(false)
    },
    onError: () => toast.error('Failed to add section.'),
  })

  return (
    <AccordionItem value={grade.id} className="rounded-lg border border-border bg-card px-4">
      <AccordionTrigger className="hover:no-underline">
        <div className="flex items-center gap-3">
          <span className="font-medium text-foreground">{grade.name}</span>
          <Badge variant="secondary">
            {grade.sections.length} section{grade.sections.length !== 1 ? 's' : ''}
          </Badge>
        </div>
      </AccordionTrigger>

      <AccordionContent>
        <div className="pt-3 pb-4 px-1 flex flex-col gap-4">
          {/* Section chips row */}
          <div className="flex flex-wrap items-center gap-2">
            {grade.sections.map((section) => (
              <SectionChip key={section.id} section={section} gradeId={grade.id} />
            ))}

            {addingSectionOpen ? (
              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  if (newSectionName.trim()) addSectionMutation.mutate(newSectionName.trim())
                }}
                className="flex items-center gap-1"
              >
                <input
                  autoFocus
                  value={newSectionName}
                  onChange={(e) => setNewSectionName(e.target.value)}
                  placeholder="Name"
                  maxLength={50}
                  className="h-7 w-24 rounded-md border border-border bg-background px-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <Button type="submit" size="sm" variant="ghost" disabled={addSectionMutation.isPending}>
                  <Check size={14} />
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  onClick={() => { setAddingSectionOpen(false); setNewSectionName('') }}
                >
                  <X size={14} />
                </Button>
              </form>
            ) : (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                className="h-7 text-muted-foreground"
                onClick={() => setAddingSectionOpen(true)}
              >
                <Plus size={13} className="mr-1" /> Add Section
              </Button>
            )}
          </div>

          {/* Grade actions row */}
          <div className="flex items-center gap-2">
            <Button size="sm" variant="outline" onClick={onEdit}>
              <Pencil size={13} className="mr-1.5" /> Edit Grade
            </Button>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-destructive hover:text-destructive"
                      disabled={grade.sections.length > 0}
                      onClick={onDelete}
                    >
                      <Trash2 size={13} className="mr-1.5" /> Delete Grade
                    </Button>
                  </span>
                </TooltipTrigger>
                {grade.sections.length > 0 && (
                  <TooltipContent>Delete all sections first</TooltipContent>
                )}
              </Tooltip>
            </TooltipProvider>
          </div>
        </div>
      </AccordionContent>
    </AccordionItem>
  )
}
