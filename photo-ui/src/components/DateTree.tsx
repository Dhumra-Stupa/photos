import { useState, useEffect } from 'react'
import { fetchDates } from '../api'
import type { YearNode } from '../types'

interface Props {
  selected: string | null
  onSelect: (date: string | null) => void
}

export default function DateTree({ selected, onSelect }: Props) {
  const [tree, setTree] = useState<YearNode[]>([])
  const [openYears, setOpenYears] = useState<Set<number>>(new Set())
  const [openMonths, setOpenMonths] = useState<Set<string>>(new Set())

  useEffect(() => {
    fetchDates()
      .then(data => {
        setTree(data)
        // Auto-expand the most recent year
        if (data.length > 0) setOpenYears(new Set([data[0].year]))
      })
      .catch(console.error)
  }, [])

  const toggleYear = (year: number) =>
    setOpenYears(prev => {
      const next = new Set(prev)
      next.has(year) ? next.delete(year) : next.add(year)
      return next
    })

  const toggleMonth = (key: string) =>
    setOpenMonths(prev => {
      const next = new Set(prev)
      next.has(key) ? next.delete(key) : next.add(key)
      return next
    })

  const pad = (n: number) => String(n).padStart(2, '0')
  const dayKey = (y: number, m: number, d: number) => `${y}-${pad(m)}-${pad(d)}`

  return (
    <ul className="date-tree">
      <li>
        <span
          className={`tree-node all ${selected === null ? 'active' : ''}`}
          onClick={() => onSelect(null)}
        >
          All Photos
        </span>
      </li>

      {tree.map(yr => (
        <li key={yr.year}>
          <span
            className="tree-node year"
            onClick={() => toggleYear(yr.year)}
          >
            {openYears.has(yr.year) ? '▾' : '▸'} {yr.year}
          </span>

          {openYears.has(yr.year) && (
            <ul>
              {yr.months.map(mo => {
                const moKey = `${yr.year}-${mo.month}`
                return (
                  <li key={mo.month}>
                    <span
                      className="tree-node month"
                      onClick={() => toggleMonth(moKey)}
                    >
                      {openMonths.has(moKey) ? '▾' : '▸'} {mo.monthName}
                    </span>

                    {openMonths.has(moKey) && (
                      <ul>
                        {mo.days.map(d => {
                          const key = dayKey(yr.year, mo.month, d)
                          return (
                            <li key={d}>
                              <span
                                className={`tree-node day ${selected === key ? 'active' : ''}`}
                                onClick={() => onSelect(key)}
                              >
                                {d}
                              </span>
                            </li>
                          )
                        })}
                      </ul>
                    )}
                  </li>
                )
              })}
            </ul>
          )}
        </li>
      ))}
    </ul>
  )
}
