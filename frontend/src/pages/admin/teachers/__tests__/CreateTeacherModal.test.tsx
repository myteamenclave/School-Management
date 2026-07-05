import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { CreateTeacherModal } from '../components/CreateTeacherModal'
import { teachersApi } from '../../../../api/teachers'
import type { TeacherDto } from '../../../../api/teachers'

vi.mock('../../../../api/teachers', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/teachers')>()
  return {
    ...actual,
    teachersApi: {
      ...actual.teachersApi,
      create: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const mockTeacher: TeacherDto = {
  id: 'new-id',
  teacherCode: '2025-000001',
  firstName: 'Nguyen',
  lastName: 'Van A',
  phone: null,
  joiningDate: '2025-09-01',
  isActive: true,
  email: 'nguyen@school.edu',
  userId: 'user-1',
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
        <CreateTeacherModal open={true} onClose={onClose} onCreated={onCreated} />
      </QueryClientProvider>
    ),
    onClose,
    onCreated,
  }
}

async function fillRequiredFields() {
  await userEvent.type(screen.getByLabelText(/first name/i), 'Nguyen')
  await userEvent.type(screen.getByLabelText(/last name/i), 'Van A')
  await userEvent.type(screen.getByLabelText(/email/i), 'nguyen@school.edu')
  await userEvent.type(screen.getByLabelText(/password/i), 'password123')
  await userEvent.type(screen.getByLabelText(/joining date/i), '2025-09-01')
}

describe('CreateTeacherModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: submits correct payload without phone', async () => {
    vi.mocked(teachersApi.create).mockResolvedValue(mockTeacher)
    renderModal()

    await fillRequiredFields()
    await userEvent.click(screen.getByRole('button', { name: /create teacher/i }))

    await vi.waitFor(() => {
      expect(vi.mocked(teachersApi.create).mock.calls.length).toBeGreaterThan(0)
    })

    const call = vi.mocked(teachersApi.create).mock.calls[0][0]
    expect(call).toMatchObject({
      firstName: 'Nguyen',
      lastName: 'Van A',
      email: 'nguyen@school.edu',
      password: 'password123',
      joiningDate: '2025-09-01',
    })
    expect(call.phone).toBeUndefined()
  })

  it('2: phone included when filled', async () => {
    vi.mocked(teachersApi.create).mockResolvedValue(mockTeacher)
    renderModal()

    await fillRequiredFields()
    await userEvent.type(screen.getByLabelText(/phone/i), '0901234567')
    await userEvent.click(screen.getByRole('button', { name: /create teacher/i }))

    await vi.waitFor(() => {
      expect(vi.mocked(teachersApi.create).mock.calls.length).toBeGreaterThan(0)
    })
    expect(vi.mocked(teachersApi.create).mock.calls[0][0].phone).toBe('0901234567')
  })

  it('3: validation blocks short password', async () => {
    renderModal()

    await userEvent.type(screen.getByLabelText(/first name/i), 'Nguyen')
    await userEvent.type(screen.getByLabelText(/last name/i), 'Van A')
    await userEvent.type(screen.getByLabelText(/email/i), 'nguyen@school.edu')
    await userEvent.type(screen.getByLabelText(/password/i), 'abc')
    await userEvent.type(screen.getByLabelText(/joining date/i), '2025-09-01')
    await userEvent.click(screen.getByRole('button', { name: /create teacher/i }))

    await screen.findByText(/at least 8 characters/i)
    expect(vi.mocked(teachersApi.create)).not.toHaveBeenCalled()
  })

  it('4: validation blocks invalid email', async () => {
    renderModal()

    await userEvent.type(screen.getByLabelText(/first name/i), 'Nguyen')
    await userEvent.type(screen.getByLabelText(/last name/i), 'Van A')
    await userEvent.type(screen.getByLabelText(/email/i), 'notanemail')
    await userEvent.type(screen.getByLabelText(/password/i), 'password123')
    await userEvent.type(screen.getByLabelText(/joining date/i), '2025-09-01')
    await userEvent.click(screen.getByRole('button', { name: /create teacher/i }))

    await screen.findByText(/invalid email/i)
    expect(vi.mocked(teachersApi.create)).not.toHaveBeenCalled()
  })

  it('5: disables submit while mutation is pending', async () => {
    vi.mocked(teachersApi.create).mockImplementation(() => new Promise(() => {}))
    renderModal()

    await fillRequiredFields()
    await userEvent.click(screen.getByRole('button', { name: /create teacher/i }))

    await screen.findByRole('button', { name: /creating/i })
    expect(screen.getByRole('button', { name: /creating/i })).toBeDisabled()
  })

  it('6: closes and calls onCreated on success', async () => {
    vi.mocked(teachersApi.create).mockResolvedValue(mockTeacher)
    const { onClose, onCreated } = renderModal()

    await fillRequiredFields()
    await userEvent.click(screen.getByRole('button', { name: /create teacher/i }))

    await vi.waitFor(() => {
      expect(onClose).toHaveBeenCalled()
      expect(onCreated).toHaveBeenCalled()
    })
  })
})
