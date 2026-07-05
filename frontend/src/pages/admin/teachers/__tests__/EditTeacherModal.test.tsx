import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { EditTeacherModal } from '../components/EditTeacherModal'
import { teachersApi } from '../../../../api/teachers'
import type { TeacherDto } from '../../../../api/teachers'

vi.mock('../../../../api/teachers', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/teachers')>()
  return {
    ...actual,
    teachersApi: {
      ...actual.teachersApi,
      getById: vi.fn(),
      update: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const fullTeacher: TeacherDto = {
  id: 'teacher-1',
  teacherCode: 'T2025-000001',
  firstName: 'Nguyen',
  lastName: 'Van A',
  phone: '0901234567',
  joiningDate: '2025-09-01',
  isActive: true,
  email: 'nguyen@school.edu',
  userId: 'user-1',
  createdAt: '2025-09-01T00:00:00Z',
  updatedAt: null,
}

function renderModal(teacherId: string | null = 'teacher-1', props?: { onClose?: () => void; onUpdated?: () => void }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onClose = props?.onClose ?? vi.fn()
  const onUpdated = props?.onUpdated ?? vi.fn()

  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <EditTeacherModal teacherId={teacherId} onClose={onClose} onUpdated={onUpdated} />
      </QueryClientProvider>
    ),
    onClose,
    onUpdated,
  }
}

describe('EditTeacherModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: shows loading state while fetching', async () => {
    vi.mocked(teachersApi.getById).mockImplementation(() => new Promise(() => {}))
    renderModal()

    await screen.findByText('Loading…')
    expect(screen.queryByLabelText(/first name/i)).not.toBeInTheDocument()
  })

  it('2: populates form from fetched teacher', async () => {
    vi.mocked(teachersApi.getById).mockResolvedValue(fullTeacher)
    renderModal()

    await screen.findByDisplayValue('Nguyen')
    expect(screen.getByDisplayValue('Van A')).toBeInTheDocument()
    expect(screen.getByDisplayValue('0901234567')).toBeInTheDocument()
  })

  it('3: TeacherCode shown as read-only text, not an input', async () => {
    vi.mocked(teachersApi.getById).mockResolvedValue(fullTeacher)
    renderModal()

    await screen.findByText('T2025-000001')
    expect(screen.queryByDisplayValue('T2025-000001')).toBeNull()
  })

  it('4: email shown as read-only text, not an input', async () => {
    vi.mocked(teachersApi.getById).mockResolvedValue(fullTeacher)
    renderModal()

    await screen.findByText('nguyen@school.edu')
    expect(screen.queryByDisplayValue('nguyen@school.edu')).toBeNull()
  })

  it('5: isActive checkbox reflects current active status', async () => {
    vi.mocked(teachersApi.getById).mockResolvedValue({ ...fullTeacher, isActive: false })
    renderModal()

    await screen.findByDisplayValue('Nguyen')
    const checkbox = screen.getByRole('checkbox')
    expect(checkbox).not.toBeChecked()
  })

  it('6: submits correct UpdateTeacherRequest without teacherCode or email', async () => {
    vi.mocked(teachersApi.getById).mockResolvedValue(fullTeacher)
    vi.mocked(teachersApi.update).mockResolvedValue(fullTeacher)
    renderModal('teacher-1')

    await screen.findByDisplayValue('Nguyen')

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }))

    await vi.waitFor(() => {
      expect(vi.mocked(teachersApi.update)).toHaveBeenCalledWith(
        'teacher-1',
        expect.objectContaining({
          firstName: 'Nguyen',
          lastName: 'Van A',
          isActive: true,
        })
      )
    })

    const payload = vi.mocked(teachersApi.update).mock.calls[0][1]
    expect(Object.keys(payload)).not.toContain('teacherCode')
    expect(Object.keys(payload)).not.toContain('email')
  })

  it('7: label mentions "disables login" for the isActive checkbox', async () => {
    vi.mocked(teachersApi.getById).mockResolvedValue(fullTeacher)
    renderModal()

    await screen.findByDisplayValue('Nguyen')
    expect(screen.getByText(/disables login/i)).toBeInTheDocument()
  })
})
