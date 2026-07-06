import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../../components/ui/select'
import { Controller } from 'react-hook-form'
import { feeTemplatesApi } from '../../../../api/feeTemplates'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../../api/academicYears'
import { gradesApi, GRADE_KEYS } from '../../../../api/grades'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({
  name:           z.string().min(1, 'Required').max(200),
  academicYearId: z.string().min(1, 'Required'),
  gradeId:        z.string().min(1, 'Required'),
})

type FormValues = z.infer<typeof schema>

interface CreateFeeTemplateModalProps {
  open: boolean
  onClose: () => void
}

export function CreateFeeTemplateModal({ open, onClose }: CreateFeeTemplateModalProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { control, register, handleSubmit, reset, formState: { errors, isSubmitting } } =
    useForm<FormValues>({ resolver: zodResolver(schema) })

  const { data: academicYears } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
    staleTime: Infinity,
  })

  const { data: grades } = useQuery({
    queryKey: GRADE_KEYS.all,
    queryFn: gradesApi.list,
    staleTime: Infinity,
  })

  const mutation = useMutation({
    mutationFn: feeTemplatesApi.create,
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['fee-templates'] })
      toast.success('Template created')
      reset()
      onClose()
      navigate(`/admin/fee-templates/${created.id}?edit=true`)
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const onSubmit = (data: FormValues) => mutation.mutate(data)

  const handleOpenChange = (open: boolean) => {
    if (!open) { reset(); onClose() }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-md" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>New Fee Template</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cft-name">Template Name</Label>
            <Input id="cft-name" {...register('name')} />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cft-academicYear">Academic Year</Label>
            <Controller
              name="academicYearId"
              control={control}
              render={({ field }) => (
                <Select value={field.value ?? ''} onValueChange={field.onChange}>
                  <SelectTrigger id="cft-academicYear">
                    <SelectValue placeholder="Select year…" />
                  </SelectTrigger>
                  <SelectContent>
                    {academicYears?.map((y) => (
                      <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.academicYearId && <p className="text-xs text-destructive">{errors.academicYearId.message}</p>}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cft-grade">Grade</Label>
            <Controller
              name="gradeId"
              control={control}
              render={({ field }) => (
                <Select value={field.value ?? ''} onValueChange={field.onChange}>
                  <SelectTrigger id="cft-grade">
                    <SelectValue placeholder="Select grade…" />
                  </SelectTrigger>
                  <SelectContent>
                    {grades?.map((g) => (
                      <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.gradeId && <p className="text-xs text-destructive">{errors.gradeId.message}</p>}
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting || mutation.isPending}>
              {isSubmitting || mutation.isPending ? 'Creating…' : 'Create Template'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
