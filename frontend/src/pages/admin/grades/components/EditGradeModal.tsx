import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../../components/ui/dialog'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import { Label } from '../../../../components/ui/label'
import type { GradeDto } from '../../../../api/grades'
import { gradesApi, GRADE_KEYS } from '../../../../api/grades'

const schema = z.object({
  name: z.string().min(1, 'Required').max(100),
  displayOrder: z.coerce.number().int().min(0, 'Must be 0 or greater'),
})

type FormInput = z.input<typeof schema>
type FormValues = z.infer<typeof schema>

interface EditGradeModalProps {
  grade: GradeDto | null
  onClose: () => void
}

export function EditGradeModal({ grade, onClose }: EditGradeModalProps) {
  const queryClient = useQueryClient()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (grade) reset({ name: grade.name, displayOrder: grade.displayOrder })
  }, [grade, reset])

  const mutation = useMutation({
    mutationFn: (data: FormValues) => gradesApi.update(grade!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })
      toast.success('Grade updated')
      onClose()
    },
    onError: (err) => {
      if (isAxiosError(err) && err.response?.status === 409) {
        toast.error('A grade with this name already exists.')
      } else if (isAxiosError(err) && err.response?.data?.error) {
        toast.error(err.response.data.error)
      } else {
        toast.error('An unexpected error occurred.')
      }
    },
  })

  const onSubmit = (data: FormValues) => mutation.mutateAsync(data).catch(() => {})

  const handleOpenChange = (open: boolean) => {
    if (!open) onClose()
  }

  return (
    <Dialog open={grade !== null} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Edit Grade</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="eg-name">Grade Name</Label>
            <Input id="eg-name" {...register('name')} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="eg-order">Display Order</Label>
            <Input id="eg-order" type="number" min="0" {...register('displayOrder')} />
            {errors.displayOrder && (
              <p className="text-xs text-destructive">{errors.displayOrder.message}</p>
            )}
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save Changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
