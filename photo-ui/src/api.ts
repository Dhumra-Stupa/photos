import type { Photo, YearNode } from './types'

export async function fetchPhotos(
  date: string | null,
  lens: string | null,
  focalLength: number | null,
  offset = 0,
  limit = 60,
): Promise<Photo[]> {
  const params = new URLSearchParams()
  if (date) params.set('date', date)
  if (lens) params.set('lens', lens)
  if (focalLength != null) params.set('focalLength', String(focalLength))
  params.set('offset', String(offset))
  params.set('limit', String(limit))
  const res = await fetch(`/api/photos?${params}`)
  if (!res.ok) throw new Error(`fetchPhotos failed: ${res.status}`)
  return res.json()
}

export async function fetchPhotoCount(
  date: string | null,
  lens: string | null,
  focalLength: number | null,
): Promise<number> {
  const params = new URLSearchParams()
  if (date) params.set('date', date)
  if (lens) params.set('lens', lens)
  if (focalLength != null) params.set('focalLength', String(focalLength))
  const res = await fetch(`/api/photos/count?${params}`)
  if (!res.ok) throw new Error(`fetchPhotoCount failed: ${res.status}`)
  return res.json()
}

export async function fetchFocalLengths(
  date: string | null,
  lens: string | null,
): Promise<number[]> {
  const params = new URLSearchParams()
  if (date) params.set('date', date)
  if (lens) params.set('lens', lens)
  const res = await fetch(`/api/focal-lengths?${params}`)
  if (!res.ok) throw new Error(`fetchFocalLengths failed: ${res.status}`)
  return res.json()
}

export async function fetchDates(): Promise<YearNode[]> {
  const res = await fetch('/api/dates')
  if (!res.ok) throw new Error(`fetchDates failed: ${res.status}`)
  return res.json()
}

export async function fetchLenses(): Promise<string[]> {
  const res = await fetch('/api/lenses')
  if (!res.ok) throw new Error(`fetchLenses failed: ${res.status}`)
  return res.json()
}

export function imageUrl(fullPath: string): string {
  return `/api/image?path=${encodeURIComponent(fullPath)}`
}

export function thumbUrl(fullPath: string): string {
  return `/api/thumb?path=${encodeURIComponent(fullPath)}`
}
