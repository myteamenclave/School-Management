import { useEffect } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
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
import { subjectsApi, SUBJECT_KEYS } from '../../../../api/subjects'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({
  name:        z.string().min(1, 'Required').max(200),
  description: z.string().max(500).optional(),
  isActive:    z.boolean(),
})

type FormValues = z.infer<typeof schema>

interface EditSubjectModalProps {
  subjectId: string | null
  onClose: () => void
  onUpdated: () => void
}

export function EditSubjectModal({ subjectId, onClose, onUpdated }: EditSubjectModalProps) {
  const open = subjectId !== null

  const { data: subject, isLoading } = useQuery({
    queryKey: SUBJECT_KEYS.detail(subjectId ?? ''),
    queryFn: () => subjectsApi.getById(subjectId!),
    enabled: subjectId !== null,
  })

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (subject) {
      reset({
        name:        subject.name,
        description: subject.description ?? '',
        isActive:    subject.isActive,
      })
    }
  }, [subject, reset])

  const mutation = useMutation({
    mutationFn: (data: FormValues) =>
      subjectsApi.update(subjectId!, {
        name:        data.name,
        description: data.description || undefined,
        isActive:    data.isActive,
      }),
    onSuccess: () => {
      toast.success('Subject updated')
      onUpdated()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const onSubmit = (data: FormValues) => mutation.mutate(data)

  const handleOpenChange = (open: boolean) => {
    if (!open) onClose()
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-md" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Edit Subject</DialogTitle>
          {subject && (
            <p className="text-xs text-muted-foreground font-mono mt-0.5">{subject.code}</p>
          )}
        </DialogHeader>

        {isLoading ? (
          <div className="flex items-center justify-center h-32 text-sm text-muted-foreground">
            Loading…
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="es-name">Name</Label>
              <Input id="es-name" {...register('name')} />
              {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="es-description">Description <span className="text-muted-foreground font-normal">(optional)</span></Label>
              <Input id="es-description" {...register('description')} />
              {errors.description && <p className="text-xs text-destructive">{errors.description.message}</p>}
            </div>

            <Controller
              name="isActive"
              control={control}
              render={({ field }) => (
                <div className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    id="es-isActive"
                    checked={field.value ?? false}
                    onChange={(e) => field.onChange(e.target.checked)}
                    className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                  />
                  <label htmlFor="es-isActive" className="text-sm text-foreground cursor-pointer">
                    Active
                  </label>
                </div>
              )}
            />

            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={isSubmitting || mutation.isPending}>
                {isSubmitting || mutation.isPending ? 'Saving…' : 'Save Changes'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
