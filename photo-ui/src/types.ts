export interface Photo {
  fullPath: string
  relativePath: string
  sizeBytes: number
  lastModified: string
  dateTaken: string | null
  cameraModel: string | null
  lensModel: string | null
  fNumber: number | null
  exposureTimeMs: number | null
  isoSpeed: number | null
  focalLengthMm: number | null
}

export interface MonthNode {
  month: number
  monthName: string
  days: number[]
}

export interface YearNode {
  year: number
  months: MonthNode[]
}
