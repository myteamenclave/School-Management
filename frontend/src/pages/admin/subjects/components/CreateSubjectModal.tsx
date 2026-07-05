import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
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
import { subjectsApi } from '../../../../api/subjects'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({
  name:        z.string().min(1, 'Required').max(200),
  code:        z.string().min(1, 'Required').max(20)
                 .regex(/^[A-Za-z0-9_-]+$/, 'Only letters, numbers, hyphens, or underscores'),
  description: z.string().max(500).optional(),
})

type FormValues = z.infer<typeof schema>

interface CreateSubjectModalProps {
  open: boolean
  onClose: () => void
  onCreated: () => void
}

export function CreateSubjectModal({ open, onClose, onCreated }: CreateSubjectModalProps) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const mutation = useMutation({
    mutationFn: subjectsApi.create,
    onSuccess: () => {
      toast.success('Subject created')
      reset()
      onCreated()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const onSubmit = (data: FormValues) =>
    mutation.mutate({
      name:        data.name,
      code:        data.code,
      description: data.description || undefined,
    })

  const handleOpenChange = (open: boolean) => {
    if (!open) { reset(); onClose() }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-md" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Add Subject</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cs-name">Name</Label>
            <Input id="cs-name" placeholder="e.g. Mathematics" {...register('name')} />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cs-code">Code</Label>
            <Input id="cs-code" placeholder="e.g. MATH101" {...register('code')} />
            <p className="text-xs text-muted-foreground">Letters, numbers, hyphens, or underscores. Cannot be changed later.</p>
            {errors.code && <p className="text-xs text-destructive">{errors.code.message}</p>}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cs-description">Description <span className="text-muted-foreground font-normal">(optional)</span></Label>
            <Input id="cs-description" {...register('description')} />
            {errors.description && <p className="text-xs text-destructive">{errors.description.message}</p>}
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting || mutation.isPending}>
              {isSubmitting || mutation.isPending ? 'Creating…' : 'Create Subject'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
