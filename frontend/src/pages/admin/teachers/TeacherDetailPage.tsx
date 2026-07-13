import { useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { ArrowLeft } from 'lucide-react'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '../../../components/ui/tabs'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
import { AssignmentsTab } from './components/AssignmentsTab'
import { teachersApi, TEACHER_KEYS } from '../../../api/teachers'

export const teacherDetailSchema = z.object({
  firstName:   z.string().min(1, 'Required').max(100),
  lastName:    z.string().min(1, 'Required').max(100),
  joiningDate: z.string().min(1, 'Required'),
  phone:       z.string().max(20).optional(),
  isActive:    z.boolean(),
})

export type TeacherDetailFormValues = z.infer<typeof teacherDetailSchema>

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const dateInputClass =
  'flex h-11 w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

const clampYear = (e: React.FormEvent<HTMLInputElement>) => {
  const input = e.currentTarget
  if (!input.value) return
  const [year, ...rest] = input.value.split('-')
  if (year.length > 4) input.value = year.slice(0, 4) + (rest.length ? '-' + rest.join('-') : '')
}

interface TeacherDetailsFormProps {
  teacherId: string
}

function TeacherDetailsForm({ teacherId }: TeacherDetailsFormProps) {
  const queryClient = useQueryClient()

  const { data: teacher } = useQuery({
    queryKey: TEACHER_KEYS.detail(teacherId),
    queryFn: () => teachersApi.getById(teacherId),
  })

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<TeacherDetailFormValues>({ resolver: zodResolver(teacherDetailSchema) })

  useEffect(() => {
    if (teacher) {
      reset({
        firstName:   teacher.firstName,
        lastName:    teacher.lastName,
        joiningDate: teacher.joiningDate,
        phone:       teacher.phone ?? '',
        isActive:    teacher.isActive,
      })
    }
  }, [teacher, reset])

  const mutation = useMutation({
    mutationFn: (data: TeacherDetailFormValues) =>
      teachersApi.update(teacherId, {
        firstName:   data.firstName,
        lastName:    data.lastName,
        joiningDate: data.joiningDate,
        phone:       data.phone || undefined,
        isActive:    data.isActive,
      }),
    onSuccess: () => {
      toast.success('Teacher updated')
      queryClient.invalidateQueries({ queryKey: TEACHER_KEYS.detail(teacherId) })
      queryClient.invalidateQueries({ queryKey: ['teachers'] })
    },
    onError: (err) => toast.error(extractError(err)),
  })

  return (
    <form onSubmit={handleSubmit((d) => mutation.mutate(d))} className="flex flex-col gap-4 max-w-lg">
      {teacher && (
        <p className="text-sm text-muted-foreground -mt-2">{teacher.email}</p>
      )}

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="td-firstName">First Name</Label>
          <Input id="td-firstName" {...register('firstName')} />
          {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="td-lastName">Last Name</Label>
          <Input id="td-lastName" {...register('lastName')} />
          {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="td-joiningDate">Joining Date</Label>
          <input
            id="td-joiningDate"
            type="date"
            className={dateInputClass}
            onInput={clampYear}
            {...register('joiningDate')}
          />
          {errors.joiningDate && <p className="text-xs text-destructive">{errors.joiningDate.message}</p>}
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="td-phone">Phone</Label>
          <Input id="td-phone" {...register('phone')} />
          {errors.phone && <p className="text-xs text-destructive">{errors.phone.message}</p>}
        </div>
      </div>

      <Controller
        name="isActive"
        control={control}
        render={({ field }) => (
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="td-isActive"
              checked={field.value ?? false}
              onChange={(e) => field.onChange(e.target.checked)}
              className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
            />
            <label htmlFor="td-isActive" className="text-sm text-foreground cursor-pointer">
              Active (unchecking disables login)
            </label>
          </div>
        )}
      />

      <div className="flex justify-end pt-2">
        <Button type="submit" disabled={isSubmitting || mutation.isPending}>
          {isSubmitting || mutation.isPending ? 'Saving…' : 'Save Changes'}
        </Button>
      </div>
    </form>
  )
}

export function TeacherDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const { data: teacher, isLoading, isError } = useQuery({
    queryKey: TEACHER_KEYS.detail(id ?? ''),
    queryFn: () => teachersApi.getById(id!),
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
        Loading…
      </div>
    )
  }

  if (isError || !teacher) {
    return (
      <div className="p-8 text-destructive text-sm">
        Failed to load teacher. <button className="underline" onClick={() => navigate('/admin/teachers')}>Go back</button>
      </div>
    )
  }

  return (
    <div className="px-8 py-8 max-w-4xl mx-auto">
      {/* Back row */}
      <div className="mb-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/admin/teachers')} className="-ml-2">
          <ArrowLeft size={16} className="mr-1" /> Back
        </Button>
      </div>

      {/* Header */}
      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold text-foreground">
          {teacher.firstName} {teacher.lastName}
        </h1>
        <span className="font-mono text-xs text-muted-foreground">{teacher.teacherCode}</span>
      </div>

      <Tabs defaultValue="details">
        <TabsList className="mb-6">
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="assignments">Assignments</TabsTrigger>
        </TabsList>

        <TabsContent value="details">
          <TeacherDetailsForm teacherId={id!} />
        </TabsContent>

        <TabsContent value="assignments">
          <AssignmentsTab teacherId={id!} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
