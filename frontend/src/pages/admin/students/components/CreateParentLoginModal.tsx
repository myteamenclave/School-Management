import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { CheckCircle2, Copy } from 'lucide-react'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '../../../../components/ui/dialog'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import { Label } from '../../../../components/ui/label'
import {
  parentAccountsApi,
  type ParentLoginResultDto,
} from '../../../../api/parentAccounts'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({
  temporaryPassword: z.string().min(8, 'At least 8 characters').max(128, 'At most 128 characters'),
})
type FormValues = z.infer<typeof schema>

interface CreateParentLoginModalProps {
  open: boolean
  studentId: string
  guardianEmail: string
  onClose: () => void
  onCreated: () => void
}

function CopyRow({ label, value }: { label: string; value: string }) {
  const copy = async () => {
    await navigator.clipboard.writeText(value)
    toast.success('Copied')
  }
  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-muted/40 px-3 py-2">
      <div className="flex flex-col">
        <span className="text-xs text-muted-foreground">{label}</span>
        <span className="font-mono text-sm text-foreground break-all">{value}</span>
      </div>
      <Button type="button" variant="ghost" size="sm" className="h-8 w-8 p-0 shrink-0" onClick={copy}>
        <Copy size={14} />
      </Button>
    </div>
  )
}

export function CreateParentLoginModal({
  open, studentId, guardianEmail, onClose, onCreated,
}: CreateParentLoginModalProps) {
  const [result, setResult] = useState<ParentLoginResultDto | null>(null)
  const [submittedPassword, setSubmittedPassword] = useState('')

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { temporaryPassword: '' },
  })

  useEffect(() => {
    if (open) {
      reset({ temporaryPassword: '' })
      setResult(null)
      setSubmittedPassword('')
    }
  }, [open, reset])

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      parentAccountsApi.createLogin(studentId, { temporaryPassword: values.temporaryPassword }),
    onSuccess: (res, values) => {
      setSubmittedPassword(values.temporaryPassword)
      setResult(res)
      onCreated()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleDone = () => {
    onClose()
  }

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose() }}>
      <DialogContent className="sm:max-w-md">
        {result === null ? (
          <>
            <DialogHeader>
              <DialogTitle>Create Parent Login</DialogTitle>
            </DialogHeader>

            <form
              id="create-parent-login-form"
              onSubmit={handleSubmit((v) => mutation.mutate(v))}
              className="flex flex-col gap-4 mt-2"
            >
              <div className="flex flex-col gap-1.5">
                <Label>Guardian Email</Label>
                <Input value={guardianEmail} readOnly disabled className="font-mono text-sm" />
                <p className="text-xs text-muted-foreground">
                  The parent logs in with this email — it's taken from the student record.
                </p>
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="cpl-password">Temporary Password</Label>
                <Input id="cpl-password" type="text" autoComplete="off" {...register('temporaryPassword')} />
                {errors.temporaryPassword ? (
                  <p className="text-xs text-destructive">{errors.temporaryPassword.message}</p>
                ) : (
                  <p className="text-xs text-muted-foreground">
                    The parent uses this to log in. Share it with them directly — it won't be emailed.
                  </p>
                )}
              </div>
            </form>

            <DialogFooter>
              <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>Cancel</Button>
              <Button type="submit" form="create-parent-login-form" disabled={mutation.isPending}>
                {mutation.isPending ? 'Creating…' : 'Create Login'}
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <CheckCircle2 size={18} className="text-green-600 dark:text-green-500" />
                {result.accountCreated ? 'Parent login ready' : 'Linked to existing parent account'}
              </DialogTitle>
            </DialogHeader>

            <div className="flex flex-col gap-3 mt-2">
              <CopyRow label="Email" value={result.email} />

              {result.accountCreated ? (
                <>
                  <CopyRow label="Password" value={submittedPassword} />
                  <p className="text-xs text-muted-foreground">
                    Share these with the parent — they won't be emailed.
                  </p>
                </>
              ) : (
                <>
                  <p className="text-xs text-muted-foreground">
                    This parent already had an account, so the password you entered was <strong>not</strong> applied —
                    their existing password is unchanged.
                  </p>
                  {!result.linkCreated && (
                    <p className="text-xs text-muted-foreground">
                      This parent was already linked to this student.
                    </p>
                  )}
                </>
              )}
            </div>

            <DialogFooter>
              <Button onClick={handleDone}>Done</Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
