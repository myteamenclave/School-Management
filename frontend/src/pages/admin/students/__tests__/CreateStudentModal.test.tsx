import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { CreateStudentModal } from '../components/CreateStudentModal'
import { studentsApi } from '../../../../api/students'

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
      create: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const mockStudent = {
  id: 'new-id',
  studentCode: '2025-000001',
  firstName: 'Nguyen',
  lastName: 'Van A',
  dateOfBirth: '2010-01-15',
  gender: 'Male',
  enrollmentDate: '2025-09-01',
  enrollmentStatus: 'Active',
  guardianName: null,
  guardianPhone: null,
  guardianEmail: null,
  createdAt: '2025-09-01T00:00:00Z',
  updatedAt: null,
}

function renderModal(props?: { onClose?: () => void; onCreated?: () => void }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onClose = props?.onClose ?? vi.fn()
  const onCreated = props?.onCreated ?? vi.fn()

  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <CreateStudentModal open={true} onClose={onClose} onCreated={onCreated} />
      </QueryClientProvider>
    ),
    onClose,
    onCreated,
  }
}

async function fillRequiredFields() {
  await userEvent.type(screen.getByLabelText(/first name/i), 'Nguyen')
  await userEvent.type(screen.getByLabelText(/last name/i), 'Van A')
  await userEvent.type(screen.getByLabelText(/date of birth/i), '2010-01-15')
  await userEvent.selectOptions(screen.getByRole('combobox'), 'Male')
  await userEvent.type(screen.getByLabelText(/enrollment date/i), '2025-09-01')
}

describe('CreateStudentModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: submits correct payload without guardian fields', async () => {
    vi.mocked(studentsApi.create).mockResolvedValue(mockStudent)
    renderModal()

    await fillRequiredFields()
    await userEvent.click(screen.getByRole('button', { name: /create student/i }))

    await vi.waitFor(() => {
      expect(vi.mocked(studentsApi.create).mock.calls.length).toBeGreaterThan(0)
    })

    const call = vi.mocked(studentsApi.create).mock.calls[0][0]
    expect(call).toMatchObject({ firstName: 'Nguyen', lastName: 'Van A', gender: 'Male' })
    expect(call.guardianName).toBeUndefined()
    expect(call.guardianPhone).toBeUndefined()
    expect(call.guardianEmail).toBeUndefined()
  })

  it('2: submits with guardian fields', async () => {
    vi.mocked(studentsApi.create).mockResolvedValue(mockStudent)
    renderModal()

    await fillRequiredFields()
    await userEvent.type(screen.getByLabelText(/guardian name/i), 'Tran Van B')
    await userEvent.type(screen.getByLabelText(/guardian phone/i), '0901234567')
    await userEvent.type(screen.getByLabelText(/guardian email/i), 'guardian@example.com')
    await userEvent.click(screen.getByRole('button', { name: /create student/i }))

    await vi.waitFor(() => {
      expect(vi.mocked(studentsApi.create).mock.calls.length).toBeGreaterThan(0)
    })
    const call = vi.mocked(studentsApi.create).mock.calls[0][0]
    expect(call.guardianName).toBe('Tran Van B')
    expect(call.guardianPhone).toBe('0901234567')
    expect(call.guardianEmail).toBe('guardian@example.com')
  })

  it('3: validation blocks empty first name', async () => {
    renderModal()

    await userEvent.click(screen.getByRole('button', { name: /create student/i }))

    await screen.findAllByText('Required')
    expect(vi.mocked(studentsApi.create)).not.toHaveBeenCalled()
  })

  it('4: disables submit while mutation is pending', async () => {
    vi.mocked(studentsApi.create).mockImplementation(() => new Promise(() => {}))
    renderModal()

    await fillRequiredFields()
    await userEvent.click(screen.getByRole('button', { name: /create student/i }))

    await screen.findByRole('button', { name: /creating/i })
    expect(screen.getByRole('button', { name: /creating/i })).toBeDisabled()
  })

  it('5: closes and resets on success', async () => {
    vi.mocked(studentsApi.create).mockResolvedValue(mockStudent)
    const { onClose, onCreated } = renderModal()

    await fillRequiredFields()
    await userEvent.click(screen.getByRole('button', { name: /create student/i }))

    await vi.waitFor(() => {
      expect(onClose).toHaveBeenCalled()
      expect(onCreated).toHaveBeenCalled()
    })
  })
})
