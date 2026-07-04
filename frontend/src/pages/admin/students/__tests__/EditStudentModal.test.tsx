import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { EditStudentModal } from '../components/EditStudentModal'
import { studentsApi } from '../../../../api/students'
import type { StudentDto } from '../../../../api/students'

vi.mock('../../../../components/ui/select', () => ({
  Select: ({ onValueChange, value, children }: { onValueChange: (v: string) => void; value: string; children: React.ReactNode }) => (
    <select value={value ?? ''} onChange={(e) => onValueChange(e.target.value)}>{children}</select>
  ),
  SelectTrigger: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectValue: ({ placeholder }: { placeholder?: string }) => <>{placeholder ?? ''}</>,
  SelectContent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children: React.ReactNode }) => <option value={value}>{children}</option>,
}))

vi.mock('../../../../api/students', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/students')>()
  return {
    ...actual,
    studentsApi: {
      ...actual.studentsApi,
      getById: vi.fn(),
      update: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const fullStudent: StudentDto = {
  id: 'student-1',
  studentCode: '2025-000001',
  firstName: 'Nguyen',
  lastName: 'Van A',
  dateOfBirth: '2010-01-15',
  gender: 'Male',
  enrollmentDate: '2025-09-01',
  enrollmentStatus: 'Active',
  guardianName: 'Tran Van B',
  guardianPhone: '0901234567',
  guardianEmail: 'guardian@example.com',
  createdAt: '2025-09-01T00:00:00Z',
  updatedAt: null,
}

function renderModal(studentId: string | null = 'student-1', props?: { onClose?: () => void; onUpdated?: () => void }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onClose = props?.onClose ?? vi.fn()
  const onUpdated = props?.onUpdated ?? vi.fn()

  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <EditStudentModal studentId={studentId} onClose={onClose} onUpdated={onUpdated} />
      </QueryClientProvider>
    ),
    onClose,
    onUpdated,
  }
}

describe('EditStudentModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: shows loading state while fetching', async () => {
    vi.mocked(studentsApi.getById).mockImplementation(() => new Promise(() => {}))
    renderModal()

    await screen.findByText('Loading…')
    expect(screen.queryByLabelText(/first name/i)).not.toBeInTheDocument()
  })

  it('2: populates form from fetched student', async () => {
    vi.mocked(studentsApi.getById).mockResolvedValue(fullStudent)
    renderModal()

    await screen.findByDisplayValue('Nguyen')
    expect(screen.getByDisplayValue('Van A')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Tran Van B')).toBeInTheDocument()
  })

  it('3: StudentCode shown as read-only text, not an input', async () => {
    vi.mocked(studentsApi.getById).mockResolvedValue(fullStudent)
    renderModal()

    await screen.findByText('2025-000001')

    // It should appear as text, not as an input field with that value
    const inputWithCode = screen.queryByDisplayValue('2025-000001')
    expect(inputWithCode).toBeNull()
  })

  it('4: EnrollmentStatus dropdown present with current value', async () => {
    vi.mocked(studentsApi.getById).mockResolvedValue(fullStudent)
    renderModal()

    await screen.findByDisplayValue('Nguyen')
    // The select should show the current status value
    expect(screen.getByDisplayValue('Active')).toBeInTheDocument()
  })

  it('5: submits correct UpdateStudentRequest without studentCode', async () => {
    vi.mocked(studentsApi.getById).mockResolvedValue(fullStudent)
    vi.mocked(studentsApi.update).mockResolvedValue(fullStudent)
    renderModal('student-1')

    // Wait for form to be populated from the fetched student
    await screen.findByDisplayValue('Nguyen')
    await screen.findByDisplayValue('Active')

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }))

    await vi.waitFor(() => {
      expect(vi.mocked(studentsApi.update)).toHaveBeenCalledWith(
        'student-1',
        expect.objectContaining({
          firstName: 'Nguyen',
          lastName: 'Van A',
          enrollmentStatus: 'Active',
        })
      )
    })

    const payload = vi.mocked(studentsApi.update).mock.calls[0][1]
    expect(Object.keys(payload)).not.toContain('studentCode')
  })
})
