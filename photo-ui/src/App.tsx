import { useState, useEffect } from 'react'
import DateTree from './components/DateTree'
import PhotoGrid from './components/PhotoGrid'
import { fetchCameras, fetchLenses, fetchFocalLengths } from './api'
import './App.css'

export default function App() {
  const [selectedDate, setSelectedDate] = useState<string | null>(null)
  const [selectedCamera, setSelectedCamera] = useState<string | null>(null)
  const [selectedLens, setSelectedLens] = useState<string | null>(null)
  const [selectedFocalLength, setSelectedFocalLength] = useState<number | null>(null)
  const [cameras, setCameras] = useState<string[]>([])
  const [lenses, setLenses] = useState<string[]>([])
  const [focalLengths, setFocalLengths] = useState<number[]>([])

  // Cameras are global — load once
  useEffect(() => {
    fetchCameras().then(setCameras).catch(console.error)
  }, [])

  // When camera changes: reset lens + reload lens list scoped to that camera
  useEffect(() => {
    setSelectedLens(null)
    fetchLenses(selectedCamera).then(setLenses).catch(console.error)
  }, [selectedCamera])

  // When date, camera, or lens changes: reset focal length + reload focal length list
  useEffect(() => {
    setSelectedFocalLength(null)
    fetchFocalLengths(selectedDate, selectedCamera, selectedLens)
      .then(setFocalLengths)
      .catch(console.error)
  }, [selectedDate, selectedCamera, selectedLens])

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-header">Browse</div>
        <DateTree selected={selectedDate} onSelect={setSelectedDate} />
      </aside>

      <main className="content">
        <div className="toolbar">
          <label htmlFor="camera-filter">Camera</label>
          <select
            id="camera-filter"
            value={selectedCamera ?? ''}
            onChange={e => setSelectedCamera(e.target.value || null)}
          >
            <option value="">All cameras</option>
            {cameras.map(c => (
              <option key={c} value={c}>{c}</option>
            ))}
          </select>

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

        <PhotoGrid
          date={selectedDate}
          camera={selectedCamera}
          lens={selectedLens}
          focalLength={selectedFocalLength}
        />
      </main>
    </div>
  )
}
