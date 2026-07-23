import DOMPurify from 'dompurify'

export function sanitizeHtml(html: string) {
  return DOMPurify.sanitize(html)
}
