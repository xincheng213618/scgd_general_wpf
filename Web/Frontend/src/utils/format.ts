export function humanSize(value?: number) {
  let size = Number(value || 0)
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  for (const unit of units) {
    if (Math.abs(size) < 1024 || unit === units[units.length - 1]) {
      return `${size.toFixed(size >= 10 || unit === 'B' ? 0 : 1)} ${unit}`
    }
    size /= 1024
  }
  return '0 B'
}

export function shortDate(value?: string) {
  if (!value) return '-'
  const normalized = value.trim()
  if (/(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized)) {
    const date = new Date(normalized)
    if (!Number.isNaN(date.getTime())) {
      const pad = (part: number) => String(part).padStart(2, '0')
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
    }
  }
  return normalized.replace('T', ' ').slice(0, 16)
}

export function downloadPath(relativePath?: string) {
  return relativePath ? `/download/${relativePath}` : '#'
}

export function fileIconName(isDir?: boolean) {
  return isDir ? '目录' : '文件'
}
