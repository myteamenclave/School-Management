import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Check, X, Trash2 } from 'lucide-react'
import { Button } from '../../../../components/ui/button'
import type { SectionDto } from '../../../../api/grades'
import { gradesApi, GRADE_KEYS } from '../../../../api/grades'

interface SectionChipProps {
  section: SectionDto
  gradeId: string
}

export function SectionChip({ section, gradeId }: SectionChipProps) {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [value, setValue] = useState(section.name)

  const renameMutation = useMutation({
    mutationFn: () => gradesApi.updateSection(gradeId, section.id, { name: value }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })
      toast.success('Section renamed')
      setEditing(false)
    },
    onError: () => toast.error('Failed to rename section.'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => gradesApi.deleteSection(gradeId, section.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })
      toast.success('Section deleted')
    },
    onError: () => toast.error('Failed to delete section.'),
  })

  const handleSave = () => {
    if (!value.trim() || value === section.name) {
      handleCancel()
      return
    }
    renameMutation.mutate()
  }

  const handleCancel = () => {
    setEditing(false)
    setValue(section.name)
  }

  if (editing) {
    return (
      <span className="inline-flex items-center gap-1">
        <input
          autoFocus
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={50}
          className="h-7 w-20 rounded-md border border-ring bg-background px-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          onKeyDown={(e) => {
            if (e.key === 'Enter') handleSave()
            if (e.key === 'Escape') handleCancel()
          }}
        />
        <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={handleSave} disabled={renameMutation.isPending}>
          <Check size={13} />
        </Button>
        <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={handleCancel}>
          <X size={13} />
        </Button>
        <Button
          size="sm"
          variant="ghost"
          className="h-7 w-7 p-0 text-destructive hover:text-destructive"
          onClick={() => {
            if (window.confirm(`Delete section "${section.name}"?`)) deleteMutation.mutate()
          }}
          disabled={deleteMutation.isPending}
        >
          <Trash2 size={13} />
        </Button>
      </span>
    )
  }

  return (
    <button
      onClick={() => setEditing(true)}
      className="inline-flex h-7 items-center rounded-full border border-border bg-muted px-3 text-sm text-foreground hover:bg-accent transition-colors"
    >
      {section.name}
    </button>
  )
}
