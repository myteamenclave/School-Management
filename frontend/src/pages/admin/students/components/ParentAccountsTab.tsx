import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { UserPlus, Trash2 } from 'lucide-react'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '../../../../components/ui/table'
import { Button } from '../../../../components/ui/button'
import { parentAccountsApi, PARENT_ACCOUNT_KEYS } from '../../../../api/parentAccounts'
import { type StudentDto } from '../../../../api/students'
import { CreateParentLoginModal } from './CreateParentLoginModal'

interface ParentAccountsTabProps {
  student: StudentDto
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function ParentAccountsTab({ student }: ParentAccountsTabProps) {
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)

  const hasGuardianEmail = !!student.guardianEmail && student.guardianEmail.trim() !== ''

  const { data: parents = [], isLoading } = useQuery({
    queryKey: PARENT_ACCOUNT_KEYS.forStudent(student.id),
    queryFn: () => parentAccountsApi.list(student.id),
  })

  const removeMutation = useMutation({
    mutationFn: (parentUserId: string) => parentAccountsApi.removeLink(student.id, parentUserId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PARENT_ACCOUNT_KEYS.forStudent(student.id) })
      toast.success('Parent link removed.')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleRemove = (parentUserId: string) => {
    if (!window.confirm(
      `Remove this parent's access to ${student.firstName}? Their account will not be deleted.`,
    )) return
    removeMutation.mutate(parentUserId)
  }

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h3 className="text-sm font-semibold text-foreground">Parent Accounts</h3>
          <p className="text-xs text-muted-foreground">
            {hasGuardianEmail
              ? <>Guardian email: <span className="font-mono">{student.guardianEmail}</span></>
              : 'No guardian email set.'}
          </p>
        </div>
        <div className="flex flex-col items-end gap-1">
          <Button size="sm" onClick={() => setCreateOpen(true)} disabled={!hasGuardianEmail}>
            <UserPlus size={14} className="mr-1.5" /> Create parent login
          </Button>
          {!hasGuardianEmail && (
            <p className="text-xs text-muted-foreground text-right max-w-56">
              Add a guardian email on the Details tab to enable parent login.
            </p>
          )}
        </div>
      </div>

      {/* Linked parents */}
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Email</TableHead>
              <TableHead>Added</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={4} className="h-20 text-center text-muted-foreground text-sm">
                  Loading…
                </TableCell>
              </TableRow>
            ) : parents.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="h-20 text-center text-muted-foreground text-sm">
                  No parent accounts linked yet.
                </TableCell>
              </TableRow>
            ) : (
              parents.map((p) => (
                <TableRow key={p.parentUserId}>
                  <TableCell className="font-medium text-sm">{p.displayName}</TableCell>
                  <TableCell className="font-mono text-sm">{p.email}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {new Date(p.accountCreatedAt).toLocaleDateString()}
                  </TableCell>
                  <TableCell>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 w-7 p-0 text-destructive hover:text-destructive"
                      onClick={() => handleRemove(p.parentUserId)}
                      disabled={removeMutation.isPending}
                    >
                      <Trash2 size={13} />
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {hasGuardianEmail && (
        <CreateParentLoginModal
          open={createOpen}
          studentId={student.id}
          guardianEmail={student.guardianEmail!}
          onClose={() => setCreateOpen(false)}
          onCreated={() => {
            queryClient.invalidateQueries({ queryKey: PARENT_ACCOUNT_KEYS.forStudent(student.id) })
          }}
        />
      )}
    </div>
  )
}
