import { useEffect } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '../../../../components/ui/dialog'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '../../../../components/ui/select'
import { Button } from '../../../../components/ui/button'
import { Label } from '../../../../components/ui/label'
import { feeTemplatesApi } from '../../../../api/feeTemplates'
import { feeAssignmentsApi } from '../../../../api/feeAssignments'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({ feeTemplateId: z.string().uuid('Select a template') })
type FormValues = z.infer<typeof schema>

interface SetFeeAssignmentModalProps {
  open: boolean
  studentId: string
  academicYearId: string
  currentTemplateId?: string
  onClose: () => void
  onSaved: () => void
}

export function SetFeeAssignmentModal({
  open, studentId, academicYearId, currentTemplateId, onClose, onSaved,
}: SetFeeAssignmentModalProps) {
  const { data: templatesResult } = useQuery({
    queryKey: ['fee-templates', 'list-for-assign', academicYearId],
    queryFn: () =>
      feeTemplatesApi.list({ isActive: true, academicYearId, gradeId: null, page: 1, pageSize: 100 }),
    enabled: open,
  })

  const templates = templatesResult?.items ?? []

  const { control, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { feeTemplateId: currentTemplateId ?? '' },
  })

  useEffect(() => {
    if (open) reset({ feeTemplateId: currentTemplateId ?? '' })
  }, [open, currentTemplateId, reset])

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      feeAssignmentsApi.setStudentAssignment(studentId, {
        feeTemplateId: values.feeTemplateId,
        academicYearId,
      }),
    onSuccess: () => {
      toast.success('Fee assignment saved.')
      onSaved()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose() }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{currentTemplateId ? 'Override' : 'Assign'} Fee Template</DialogTitle>
        </DialogHeader>

        <form id="set-assignment-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="flex flex-col gap-4 mt-2">
          <div className="flex flex-col gap-1.5">
            <Label>Fee Template</Label>
            <Controller
              name="feeTemplateId"
              control={control}
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select template…" />
                  </SelectTrigger>
                  <SelectContent>
                    {templates.map((t) => (
                      <SelectItem key={t.id} value={t.id}>
                        {t.name} — {t.gradeName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.feeTemplateId && (
              <p className="text-xs text-destructive">{errors.feeTemplateId.message}</p>
            )}
          </div>
        </form>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>Cancel</Button>
          <Button type="submit" form="set-assignment-form" disabled={mutation.isPending}>
            {mutation.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
