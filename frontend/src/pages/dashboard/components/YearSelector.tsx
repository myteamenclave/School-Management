import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select'
import type { AcademicYearDto } from '../../../api/academicYears'

interface YearSelectorProps {
  years: AcademicYearDto[]
  value: string | null
  onChange: (yearId: string) => void
}

export function YearSelector({ years, value, onChange }: YearSelectorProps) {
  return (
    <Select value={value ?? undefined} onValueChange={onChange}>
      <SelectTrigger className="w-48" aria-label="Academic year">
        <SelectValue placeholder="Select year" />
      </SelectTrigger>
      <SelectContent>
        {years.map((y) => (
          <SelectItem key={y.id} value={y.id}>
            {y.name}
            {y.isCurrent ? ' (current)' : ''}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
