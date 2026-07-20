import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { CheckCircle2, Copy, RefreshCw } from 'lucide-react'
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

// Generates a strong, unambiguous temporary password (no 0/O/1/I/l).
function generatePassword(length = 14): string {
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
  const bytes = new Uint32Array(length)
  crypto.getRandomValues(bytes)
  let out = ''
  for (let i = 0; i < length; i++) out += alphabet[bytes[i] % alphabet.length]
  return out
}

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
  const [password, setPassword] = useState('')

  useEffect(() => {
    if (open) {
      setResult(null)
      setPassword(generatePassword())
    }
  }, [open])

  const mutation = useMutation({
    mutationFn: () => parentAccountsApi.createLogin(studentId, { temporaryPassword: password }),
    onSuccess: (res) => {
      setResult(res)
      onCreated()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const copyPassword = async () => {
    await navigator.clipboard.writeText(password)
    toast.success('Copied')
  }

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose() }}>
      <DialogContent className="sm:max-w-md">
        {result === null ? (
          <>
            <DialogHeader>
              <DialogTitle>Create Parent Login</DialogTitle>
            </DialogHeader>

            <div className="flex flex-col gap-4 mt-2">
              <div className="flex flex-col gap-1.5">
                <Label>Guardian Email</Label>
                <Input value={guardianEmail} readOnly disabled className="font-mono text-sm" />
                <p className="text-xs text-muted-foreground">
                  The parent logs in with this email — it's taken from the student record.
                </p>
              </div>

              <div className="flex flex-col gap-1.5">
                <Label>Temporary Password</Label>
                <div className="flex items-center gap-2">
                  <Input value={password} readOnly className="font-mono text-sm" />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-11 w-11 p-0 shrink-0"
                    title="Copy"
                    onClick={copyPassword}
                    disabled={mutation.isPending}
                  >
                    <Copy size={14} />
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-11 w-11 p-0 shrink-0"
                    title="Regenerate"
                    onClick={() => setPassword(generatePassword())}
                    disabled={mutation.isPending}
                  >
                    <RefreshCw size={14} />
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground">
                  The parent uses this to log in. Share it with them directly
                </p>
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>Cancel</Button>
              <Button onClick={() => mutation.mutate()} disabled={mutation.isPending || !password}>
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
                  <CopyRow label="Password" value={password} />
                  <p className="text-xs text-muted-foreground">
                    Share these with the parent — they won't be emailed.
                  </p>
                </>
              ) : (
                <>
                  <p className="text-xs text-muted-foreground">
                    This parent already had an account, so the generated password was <strong>not</strong> applied —
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
              <Button onClick={onClose}>Done</Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
