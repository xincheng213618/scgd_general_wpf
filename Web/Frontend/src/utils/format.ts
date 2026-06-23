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
  return value.replace('T', ' ').slice(0, 16)
}

export function downloadPath(relativePath?: string) {
  return relativePath ? `/download/${relativePath}` : '#'
}

export function fileIconName(isDir?: boolean) {
  return isDir ? '目录' : '文件'
}
