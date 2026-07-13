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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select'
import { StudentSectionAssignmentsTab } from './components/StudentSectionAssignmentsTab'
import { studentsApi, STUDENT_KEYS } from '../../../api/students'

const detailSchema = z.object({
  firstName:        z.string().min(1, 'Required').max(100),
  lastName:         z.string().min(1, 'Required').max(100),
  dateOfBirth:      z.string().min(1, 'Required'),
  gender:           z.enum(['Male', 'Female', 'Other'], { error: 'Required' }),
  enrollmentDate:   z.string().min(1, 'Required'),
  enrollmentStatus: z.enum(['Active', 'Transferred', 'Graduated', 'Dropped'], { error: 'Required' }),
  guardianName:     z.string().max(200).optional(),
  guardianPhone:    z.string().max(20).optional(),
  guardianEmail:    z.string().email('Invalid email').or(z.literal('')).optional(),
})

type DetailFormValues = z.infer<typeof detailSchema>

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

interface StudentDetailsFormProps {
  studentId: string
}

function StudentDetailsForm({ studentId }: StudentDetailsFormProps) {
  const queryClient = useQueryClient()

  const { data: student } = useQuery({
    queryKey: STUDENT_KEYS.detail(studentId),
    queryFn: () => studentsApi.getById(studentId),
  })

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<DetailFormValues>({ resolver: zodResolver(detailSchema) })

  useEffect(() => {
    if (student) {
      reset({
        firstName:        student.firstName,
        lastName:         student.lastName,
        dateOfBirth:      student.dateOfBirth,
        gender:           student.gender as 'Male' | 'Female' | 'Other',
        enrollmentDate:   student.enrollmentDate,
        enrollmentStatus: student.enrollmentStatus as 'Active' | 'Transferred' | 'Graduated' | 'Dropped',
        guardianName:     student.guardianName  ?? '',
        guardianPhone:    student.guardianPhone ?? '',
        guardianEmail:    student.guardianEmail ?? '',
      })
    }
  }, [student, reset])

  const mutation = useMutation({
    mutationFn: (data: DetailFormValues) =>
      studentsApi.update(studentId, {
        firstName:        data.firstName,
        lastName:         data.lastName,
        dateOfBirth:      data.dateOfBirth,
        gender:           data.gender,
        enrollmentDate:   data.enrollmentDate,
        enrollmentStatus: data.enrollmentStatus,
        guardianName:     data.guardianName  || undefined,
        guardianPhone:    data.guardianPhone || undefined,
        guardianEmail:    data.guardianEmail || undefined,
      }),
    onSuccess: () => {
      toast.success('Student updated')
      queryClient.invalidateQueries({ queryKey: STUDENT_KEYS.detail(studentId) })
      queryClient.invalidateQueries({ queryKey: ['students'] })
    },
    onError: (err) => toast.error(extractError(err)),
  })

  return (
    <form onSubmit={handleSubmit((d) => mutation.mutate(d))} className="flex flex-col gap-4 max-w-lg">
      {student && (
        <p className="text-sm text-muted-foreground -mt-2">{student.studentCode}</p>
      )}

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="sd-firstName">First Name</Label>
          <Input id="sd-firstName" {...register('firstName')} />
          {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="sd-lastName">Last Name</Label>
          <Input id="sd-lastName" {...register('lastName')} />
          {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="sd-dob">Date of Birth</Label>
          <input id="sd-dob" type="date" className={dateInputClass} onInput={clampYear} {...register('dateOfBirth')} />
          {errors.dateOfBirth && <p className="text-xs text-destructive">{errors.dateOfBirth.message}</p>}
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="sd-gender">Gender</Label>
          <Controller
            name="gender"
            control={control}
            render={({ field }) => (
              <Select value={field.value ?? ''} onValueChange={field.onChange}>
                <SelectTrigger id="sd-gender">
                  <SelectValue placeholder="Select gender" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Male">Male</SelectItem>
                  <SelectItem value="Female">Female</SelectItem>
                  <SelectItem value="Other">Other</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {errors.gender && <p className="text-xs text-destructive">{errors.gender.message}</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="sd-enrollDate">Enrollment Date</Label>
          <input id="sd-enrollDate" type="date" className={dateInputClass} onInput={clampYear} {...register('enrollmentDate')} />
          {errors.enrollmentDate && <p className="text-xs text-destructive">{errors.enrollmentDate.message}</p>}
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="sd-status">Enrollment Status</Label>
          <Controller
            name="enrollmentStatus"
            control={control}
            render={({ field }) => (
              <Select value={field.value ?? ''} onValueChange={field.onChange}>
                <SelectTrigger id="sd-status">
                  <SelectValue placeholder="Select status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Transferred">Transferred</SelectItem>
                  <SelectItem value="Graduated">Graduated</SelectItem>
                  <SelectItem value="Dropped">Dropped</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {errors.enrollmentStatus && <p className="text-xs text-destructive">{errors.enrollmentStatus.message}</p>}
        </div>
      </div>

      <div className="border-t border-border pt-4">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide mb-3">Guardian (optional)</p>
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="sd-guardianName">Guardian Name</Label>
            <Input id="sd-guardianName" {...register('guardianName')} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="sd-guardianPhone">Guardian Phone</Label>
              <Input id="sd-guardianPhone" {...register('guardianPhone')} />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="sd-guardianEmail">Guardian Email</Label>
              <Input id="sd-guardianEmail" type="email" {...register('guardianEmail')} />
              {errors.guardianEmail && <p className="text-xs text-destructive">{errors.guardianEmail.message}</p>}
            </div>
          </div>
        </div>
      </div>

      <div className="flex justify-end pt-2">
        <Button type="submit" disabled={isSubmitting || mutation.isPending}>
          {isSubmitting || mutation.isPending ? 'Saving…' : 'Save Changes'}
        </Button>
      </div>
    </form>
  )
}

export function StudentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const { data: student, isLoading, isError } = useQuery({
    queryKey: STUDENT_KEYS.detail(id ?? ''),
    queryFn: () => studentsApi.getById(id!),
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
        Loading…
      </div>
    )
  }

  if (isError || !student) {
    return (
      <div className="p-8 text-destructive text-sm">
        Failed to load student.{' '}
        <button className="underline" onClick={() => navigate('/admin/students')}>Go back</button>
      </div>
    )
  }

  return (
    <div className="px-8 py-8 max-w-4xl mx-auto">
      <div className="mb-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/admin/students')} className="-ml-2">
          <ArrowLeft size={16} className="mr-1" /> Back
        </Button>
      </div>

      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold text-foreground">
          {student.firstName} {student.lastName}
        </h1>
        <span className="font-mono text-xs text-muted-foreground">{student.studentCode}</span>
      </div>

      <Tabs defaultValue="details">
        <TabsList className="mb-6">
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="assignments">Section Assignments</TabsTrigger>
        </TabsList>

        <TabsContent value="details">
          <StudentDetailsForm studentId={id!} />
        </TabsContent>

        <TabsContent value="assignments">
          <StudentSectionAssignmentsTab studentId={id!} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
