import { useState, useEffect } from 'react'
import DateTree from './components/DateTree'
import PhotoGrid from './components/PhotoGrid'
import { fetchLenses, fetchFocalLengths } from './api'
import './App.css'

export default function App() {
  const [selectedDate, setSelectedDate] = useState<string | null>(null)
  const [selectedLens, setSelectedLens] = useState<string | null>(null)
  const [selectedFocalLength, setSelectedFocalLength] = useState<number | null>(null)
  const [lenses, setLenses] = useState<string[]>([])
  const [focalLengths, setFocalLengths] = useState<number[]>([])

  useEffect(() => {
    fetchLenses().then(setLenses).catch(console.error)
  }, [])

  // Reload focal lengths and reset selection whenever date or lens changes
  useEffect(() => {
    setSelectedFocalLength(null)
    fetchFocalLengths(selectedDate, selectedLens)
      .then(setFocalLengths)
      .catch(console.error)
  }, [selectedDate, selectedLens])

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-header">Browse</div>
        <DateTree selected={selectedDate} onSelect={setSelectedDate} />
      </aside>

      <main className="content">
        <div className="toolbar">
          <label htmlFor="lens-filter">Lens</label>
          <select
            id="lens-filter"
            value={selectedLens ?? ''}
            onChange={e => setSelectedLens(e.target.value || null)}
          >
            <option value="">All lenses</option>
            {lenses.map(l => (
              <option key={l} value={l}>{l}</option>
            ))}
          </select>

          {focalLengths.length > 0 && (
            <>
              <label htmlFor="focal-filter">Focal length</label>
              <select
                id="focal-filter"
                value={selectedFocalLength ?? ''}
                onChange={e => setSelectedFocalLength(e.target.value ? Number(e.target.value) : null)}
              >
                <option value="">All</option>
                {focalLengths.map(fl => (
                  <option key={fl} value={fl}>{fl} mm</option>
                ))}
              </select>
            </>
          )}
        </div>

        <PhotoGrid date={selectedDate} lens={selectedLens} focalLength={selectedFocalLength} />
      </main>
    </div>
  )
}
