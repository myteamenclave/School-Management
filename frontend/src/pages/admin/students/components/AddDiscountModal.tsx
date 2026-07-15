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
  if (isAxiosError(err) && err.response?.status === 409) return 'This discount is already assigned.'
  return 'An unexpected error occurred.'
}

const schema = z.object({ discountRuleId: z.string().uuid('Select a discount rule') })
type FormValues = z.infer<typeof schema>

interface AddDiscountModalProps {
  open: boolean
  studentId: string
  academicYearId: string
  assignedTemplateId: string
  alreadyAssignedRuleIds: Set<string>
  onClose: () => void
  onAdded: () => void
}

export function AddDiscountModal({
  open, studentId, academicYearId, assignedTemplateId, alreadyAssignedRuleIds, onClose, onAdded,
}: AddDiscountModalProps) {
  const { data: template } = useQuery({
    queryKey: ['fee-templates', 'detail', assignedTemplateId],
    queryFn: () => feeTemplatesApi.getById(assignedTemplateId),
    enabled: open && !!assignedTemplateId,
  })

  const available = (template?.discountRules ?? []).filter(
    (dr) => !alreadyAssignedRuleIds.has(dr.id)
  )

  const { control, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { discountRuleId: '' },
  })

  useEffect(() => {
    if (open) reset({ discountRuleId: '' })
  }, [open, reset])

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      feeAssignmentsApi.addStudentDiscount(studentId, {
        discountRuleId: values.discountRuleId,
        academicYearId,
      }),
    onSuccess: () => {
      toast.success('Discount added.')
      onAdded()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const formatRuleLabel = (name: string, type: string, value: number) =>
    `${name} (${type === 'Percentage' ? `${value}%` : `₱${value.toLocaleString()}`})`

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose() }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Add Discount Rule</DialogTitle>
        </DialogHeader>

        {available.length === 0 ? (
          <p className="text-sm text-muted-foreground py-2">
            All discount rules from this template have already been assigned.
          </p>
        ) : (
          <form id="add-discount-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="flex flex-col gap-4 mt-2">
            <div className="flex flex-col gap-1.5">
              <Label>Discount Rule</Label>
              <Controller
                name="discountRuleId"
                control={control}
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select rule…" />
                    </SelectTrigger>
                    <SelectContent>
                      {available.map((dr) => (
                        <SelectItem key={dr.id} value={dr.id}>
                          {formatRuleLabel(dr.name, dr.ruleType, dr.value)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.discountRuleId && (
                <p className="text-xs text-destructive">{errors.discountRuleId.message}</p>
              )}
            </div>
          </form>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>Cancel</Button>
          {available.length > 0 && (
            <Button type="submit" form="add-discount-form" disabled={mutation.isPending}>
              {mutation.isPending ? 'Adding…' : 'Add'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
