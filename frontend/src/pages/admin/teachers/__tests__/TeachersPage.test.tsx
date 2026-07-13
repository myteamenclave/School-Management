import { render, screen, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { TeachersPage } from '../TeachersPage'
import { teachersApi } from '../../../../api/teachers'
import type { TeacherSummaryDto, PagedResult } from '../../../../api/teachers'

vi.mock('../../../../api/teachers', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/teachers')>()
  return {
    ...actual,
    teachersApi: {
      list: vi.fn(),
      getById: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const makeTeacher = (overrides: Partial<TeacherSummaryDto> = {}): TeacherSummaryDto => ({
  id: 'teacher-1',
  teacherCode: '2025-000001',
  firstName: 'Nguyen',
  lastName: 'Van A',
  phone: null,
  joiningDate: '2025-09-01',
  isActive: true,
  email: 'nguyen@school.edu',
  ...overrides,
})

const makePagedResult = (items: TeacherSummaryDto[], total = items.length): PagedResult<TeacherSummaryDto> => ({
  items,
  totalCount: total,
  page: 1,
  pageSize: 20,
})

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <MemoryRouter initialEntries={['/admin/teachers']}>
      <QueryClientProvider client={queryClient}>
        <TeachersPage />
      </QueryClientProvider>
    </MemoryRouter>
  )
}

describe('TeachersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: renders table with teacher rows', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(
      makePagedResult([
        makeTeacher({ id: 't1', firstName: 'Nguyen', lastName: 'Van A' }),
        makeTeacher({ id: 't2', firstName: 'Tran', lastName: 'Thi B', email: 'tran@school.edu' }),
      ])
    )
    renderPage()

    await screen.findByText('Nguyen Van A')
    expect(screen.getByText('Tran Thi B')).toBeInTheDocument()
  })

  it('2: empty state when no teachers match', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No teachers found.')
  })

  it('3: clicking "Inactive" tab calls API with isActive: false', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No teachers found.')
    await userEvent.click(screen.getByRole('tab', { name: 'Inactive' }))

    await vi.waitFor(() => {
      const calls = vi.mocked(teachersApi.list).mock.calls
      expect(calls.some((c) => c[0].isActive === false)).toBe(true)
    })
  })

  it('4: clicking "All" tab omits isActive (passes null)', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No teachers found.')
    await userEvent.click(screen.getByRole('tab', { name: 'All' }))

    await vi.waitFor(() => {
      const calls = vi.mocked(teachersApi.list).mock.calls
      expect(calls.some((c) => c[0].isActive === null)).toBe(true)
    })
  })

  it('5: search input debounces and calls API with search param', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No teachers found.')

    const searchInput = screen.getByPlaceholderText('Search name, code, or email…')
    await userEvent.type(searchInput, 'nguyen')

    await act(async () => {
      await new Promise((r) => setTimeout(r, 350))
    })

    await vi.waitFor(() => {
      expect(vi.mocked(teachersApi.list)).toHaveBeenCalledWith(
        expect.objectContaining({ search: 'nguyen' })
      )
    })
  })

  it('6: pagination Prev disabled on page 1, Next enabled', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue({
      items: Array.from({ length: 20 }, (_, i) =>
        makeTeacher({ id: `t${i}`, teacherCode: `2025-00000${i}` })
      ),
      totalCount: 45,
      page: 1,
      pageSize: 20,
    })
    renderPage()

    await screen.findByText('Page 1 of 3')
    expect(screen.getByRole('button', { name: /prev/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /next/i })).not.toBeDisabled()
  })

  it('7: clicking edit button triggers getById with correct id', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(
      makePagedResult([makeTeacher({ id: 'teacher-abc' })])
    )
    vi.mocked(teachersApi.getById).mockResolvedValue({
      id: 'teacher-abc',
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
    })
    renderPage()

    await screen.findByText('Nguyen Van A')
    const pencilBtns = screen.getAllByRole('button').filter((b) => b.querySelector('svg'))
    await userEvent.click(pencilBtns[pencilBtns.length - 1])

    await vi.waitFor(() => {
      expect(vi.mocked(teachersApi.getById)).toHaveBeenCalledWith('teacher-abc')
    })
  })

  it('8: clicking "Add Teacher" opens create modal', async () => {
    vi.mocked(teachersApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No teachers found.')
    await userEvent.click(screen.getByRole('button', { name: /add teacher/i }))

    await screen.findByRole('dialog')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
