import { useState, useEffect, useRef, useCallback } from 'react'
import { fetchPhotos, fetchPhotoCount, imageUrl, thumbUrl } from '../api'
import type { Photo } from '../types'

const PAGE_SIZE = 60

interface Props {
  date: string | null
  camera: string | null
  lens: string | null
  focalLength: number | null
}

function tileTooltip(p: Photo): string {
  return [
    p.cameraModel,
    p.lensModel,
    p.fNumber != null ? `f/${p.fNumber.toFixed(1)}` : null,
    p.focalLengthMm != null ? `${p.focalLengthMm.toFixed(0)} mm` : null,
    p.isoSpeed != null ? `ISO ${p.isoSpeed}` : null,
    p.exposureTimeMs != null ? `${p.exposureTimeMs.toFixed(1)} ms` : null,
  ].filter(Boolean).join(' · ')
}

function tileCaption(p: Photo): string {
  if (p.dateTaken) {
    return new Date(p.dateTaken).toLocaleDateString(undefined, {
      year: 'numeric', month: 'short', day: 'numeric',
    })
  }
  return p.relativePath.split(/[\\/]/).pop() ?? p.relativePath
}

export default function PhotoGrid({ date, camera, lens, focalLength }: Props) {
  const [photos, setPhotos] = useState<Photo[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [windowStart, setWindowStart] = useState(0)
  const [sliderValue, setSliderValue] = useState(0)
  const [loading, setLoading] = useState(false)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const gridRef = useRef<HTMLDivElement>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)

  // Reset and load first page whenever any filter changes
  useEffect(() => {
    setLoading(true)
    setError(null)
    setPhotos([])
    setWindowStart(0)
    setSliderValue(0)

    Promise.all([
      fetchPhotoCount(date, camera, lens, focalLength),
      fetchPhotos(date, camera, lens, focalLength, 0, PAGE_SIZE),
    ])
      .then(([count, data]) => {
        setTotalCount(count)
        setPhotos(data)
        setLoading(false)
      })
      .catch(err => { setError(String(err)); setLoading(false) })
  }, [date, camera, lens, focalLength])

  // Sequential image loading: one at a time via a queue + IntersectionObserver
  useEffect(() => {
    const grid = gridRef.current
    if (!grid || photos.length === 0) return

    let active = 0
    const queue: Array<{ img: HTMLImageElement; src: string }> = []

    const loadNext = () => {
      if (active >= 1 || queue.length === 0) return
      const { img, src } = queue.shift()!
      active++
      const onDone = () => { active--; loadNext() }
      img.addEventListener('load', onDone, { once: true })
      img.addEventListener('error', onDone, { once: true })
      img.src = src
    }

    const observer = new IntersectionObserver(entries => {
      for (const { isIntersecting, target } of entries) {
        if (!isIntersecting) continue
        observer.unobserve(target)
        const img = target as HTMLImageElement
        const src = img.dataset.src
        if (!src) continue
        img.removeAttribute('data-src')
        queue.push({ img, src })
        loadNext()
      }
    }, { root: grid, rootMargin: '200px' })

    grid.querySelectorAll<HTMLImageElement>('img[data-src]').forEach(img => {
      observer.observe(img)
    })

    return () => observer.disconnect()
  }, [photos])

  // Infinite scroll: load next page when sentinel enters the scroll viewport
  useEffect(() => {
    const sentinel = sentinelRef.current
    const grid = gridRef.current
    if (!sentinel || !grid || loading || loadingMore) return
    if (windowStart + photos.length >= totalCount) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return
        const nextOffset = windowStart + photos.length
        setLoadingMore(true)
        fetchPhotos(date, camera, lens, focalLength, nextOffset, PAGE_SIZE)
          .then(data => {
            setPhotos(prev => [...prev, ...data])
            setLoadingMore(false)
          })
          .catch(err => { setError(String(err)); setLoadingMore(false) })
      },
      { root: grid, rootMargin: '600px' },
    )
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [photos.length, windowStart, totalCount, loading, loadingMore, date, camera, lens, focalLength])

  // Jump to a position in the collection when slider is released
  const jumpTo = useCallback((value: number) => {
    const maxStart = Math.max(0, totalCount - PAGE_SIZE)
    const offset = Math.round((value / 100) * maxStart)
    setWindowStart(offset)
    setPhotos([])
    setLoading(true)
    fetchPhotos(date, camera, lens, focalLength, offset, PAGE_SIZE)
      .then(data => { setPhotos(data); setLoading(false) })
      .catch(err => { setError(String(err)); setLoading(false) })
  }, [date, camera, lens, focalLength, totalCount])

  if (loading && photos.length === 0) return <div className="status">Loading…</div>
  if (error)   return <div className="status">Error: {error}</div>
  if (!loading && photos.length === 0) return <div className="status">No photos found.</div>

  const showSlider = totalCount > PAGE_SIZE
  const from = totalCount > 0 ? windowStart + 1 : 0
  const to = windowStart + photos.length
  const hasMore = to < totalCount

  return (
    <div className="photo-grid-container">
      {showSlider && (
        <div className="grid-nav">
          <span className="photo-count">
            {from.toLocaleString()}–{to.toLocaleString()} of {totalCount.toLocaleString()} photos
          </span>
          <input
            type="range"
            className="position-slider"
            min={0}
            max={100}
            value={sliderValue}
            onChange={e => setSliderValue(Number(e.target.value))}
            onPointerUp={() => jumpTo(sliderValue)}
            onKeyUp={() => jumpTo(sliderValue)}
            aria-label="Jump to position in photo collection"
          />
          <span className="slider-hint">oldest</span>
        </div>
      )}

      <div className="photo-grid" ref={gridRef}>
        {photos.map(p => (
          <div
            key={p.fullPath}
            className="photo-tile"
            title={tileTooltip(p)}
            onClick={() => window.open(imageUrl(p.fullPath), '_blank')}
          >
            <img
              data-src={thumbUrl(p.fullPath)}
              alt=""
              onError={e => { (e.target as HTMLImageElement).style.opacity = '0.2' }}
            />
            <div className="tile-caption">{tileCaption(p)}</div>
          </div>
        ))}

        {hasMore && <div ref={sentinelRef} className="load-sentinel" />}
        {loadingMore && <div className="load-more">Loading more…</div>}
      </div>
    </div>
  )
}
