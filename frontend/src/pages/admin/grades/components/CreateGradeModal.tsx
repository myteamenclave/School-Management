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
import { gradesApi, GRADE_KEYS } from '../../../../api/grades'

const schema = z.object({
  name: z.string().min(1, 'Required').max(100),
  displayOrder: z.coerce.number().int().min(0, 'Must be 0 or greater'),
})

type FormInput = z.input<typeof schema>
type FormValues = z.infer<typeof schema>

interface CreateGradeModalProps {
  open: boolean
  onClose: () => void
  onCreated: (id: string) => void
}

export function CreateGradeModal({ open, onClose, onCreated }: CreateGradeModalProps) {
  const queryClient = useQueryClient()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormValues>({ resolver: zodResolver(schema) })

  const mutation = useMutation({
    mutationFn: gradesApi.create,
    onSuccess: (grade) => {
      queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })
      toast.success('Grade created')
      reset()
      onCreated(grade.id)
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
    if (!open) {
      reset()
      onClose()
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-md">
        <DialogHeader>
          <DialogTitle>New Grade</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cg-name">Grade Name</Label>
            <Input id="cg-name" placeholder="e.g. Grade 1" {...register('name')} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cg-order">Display Order</Label>
            <Input
              id="cg-order"
              type="number"
              min="0"
              placeholder="0"
              {...register('displayOrder')}
            />
            {errors.displayOrder && (
              <p className="text-xs text-destructive">{errors.displayOrder.message}</p>
            )}
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Creating…' : 'Create Grade'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
