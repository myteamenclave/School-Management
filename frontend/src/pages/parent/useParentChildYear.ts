import { useState, useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { parentPortalApi, PARENT_KEYS } from '../../api/parentPortal'

// Shared bootstrap for every parent page: loads the caller's children + the year list,
// auto-selects the only child, and defaults to the current academic year. Both the grades
// and attendance pages consume this so the child/year selection logic lives in one place.
export function useParentChildYear() {
  const [childId, setChildId] = useState<string | null>(null)
  const [academicYearId, setAcademicYearId] = useState<string | null>(null)

  const {
    data: children = [],
    isLoading: childrenLoading,
    isError: childrenError,
  } = useQuery({
    queryKey: PARENT_KEYS.children(),
    queryFn: parentPortalApi.getChildren,
  })

  const { data: years = [] } = useQuery({
    queryKey: PARENT_KEYS.academicYears(),
    queryFn: parentPortalApi.getAcademicYears,
  })

  // Auto-select the only child; keep selection valid as the list resolves.
  useEffect(() => {
    if (children.length === 0) return
    setChildId((prev) => (children.some((c) => c.studentId === prev) ? prev : children[0].studentId))
  }, [children])

  // Default to the current academic year.
  useEffect(() => {
    if (years.length === 0) return
    const current = years.find((y) => y.isCurrent) ?? years[0]
    setAcademicYearId((prev) => (years.some((y) => y.id === prev) ? prev : current.id))
  }, [years])

  const selectedChild = children.find((c) => c.studentId === childId)
  const selectedYear = years.find((y) => y.id === academicYearId)

  return {
    children,
    years,
    childId,
    setChildId,
    academicYearId,
    setAcademicYearId,
    childrenLoading,
    childrenError,
    selectedChild,
    selectedYear,
  }
}
