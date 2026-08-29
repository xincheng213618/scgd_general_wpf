import {
  AuthRequiredError,
  getCsrfToken,
  parseResponse,
  redirectToLogin,
  WEB_CLIENT_HEADER_NAME,
  WEB_CLIENT_HEADER_VALUE,
} from './request'

const FILE_SAMPLE_SIZE = 64 * 1024
const DEFAULT_CHUNK_SIZE = 8 * 1024 * 1024
const MAX_CHUNK_RETRIES = 3
const TRANSFER_CLIENT_HEADER_NAME = 'X-Transfer-Client'
const TRANSFER_CLIENT_STORAGE_KEY = 'colorvision.transfer.client-id'
const TRANSFER_CLIENT_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

let fallbackTransferClientId = ''

export type TransferUploadProgress = {
  loaded: number
  total: number
  percent: number
}

export type TransferUploadOptions = {
  onProgress?: (progress: TransferUploadProgress) => void
  onResume?: (offset: number) => void
  signal?: AbortSignal
}

export type TransferUploadResult = {
  name: string
  bytes_written: number
  replaced: boolean
  download_url: string
  share_url: string
  expires_at?: string | null
  temporary?: boolean
}

type TransferUploadSession = {
  upload_id: string
  name: string
  total_size: number
  offset: number
  complete: boolean
  replaced: boolean
  download_url: string
  share_url: string
  expires_at?: string | null
  temporary?: boolean
  chunk_size?: number
}

export class UploadCanceledError extends Error {
  constructor() {
    super('上传已取消，断点已保留')
    this.name = 'UploadCanceledError'
  }
}

class TransferChunkRequestError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'TransferChunkRequestError'
    this.status = status
  }
}

class TransferChunkNetworkError extends Error {
  constructor() {
    super('网络连接中断')
    this.name = 'TransferChunkNetworkError'
  }
}

function throwIfCanceled(signal?: AbortSignal) {
  if (signal?.aborted) throw new UploadCanceledError()
}

function joinBytes(parts: Uint8Array[]) {
  const result = new Uint8Array(parts.reduce((total, part) => total + part.byteLength, 0))
  let offset = 0
  parts.forEach((part) => {
    result.set(part, offset)
    offset += part.byteLength
  })
  return result
}

function createTransferClientId() {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

function getTransferClientId() {
  try {
    const stored = localStorage.getItem(TRANSFER_CLIENT_STORAGE_KEY)
    if (stored && TRANSFER_CLIENT_ID_PATTERN.test(stored)) return stored
    const created = createTransferClientId()
    localStorage.setItem(TRANSFER_CLIENT_STORAGE_KEY, created)
    return created
  } catch {
    if (!fallbackTransferClientId) fallbackTransferClientId = createTransferClientId()
    return fallbackTransferClientId
  }
}

async function fileFingerprint(file: File, signal?: AbortSignal) {
  throwIfCanceled(signal)
  const metadata = new TextEncoder().encode(`${file.name}\u0000${file.size}\u0000${file.lastModified}`)
  const first = new Uint8Array(await file.slice(0, FILE_SAMPLE_SIZE).arrayBuffer())
  throwIfCanceled(signal)
  const lastStart = Math.max(0, file.size - FILE_SAMPLE_SIZE)
  const last = new Uint8Array(await file.slice(lastStart).arrayBuffer())
  throwIfCanceled(signal)
  const digest = await crypto.subtle.digest('SHA-256', joinBytes([metadata, first, last]))
  throwIfCanceled(signal)
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('')
}

function webHeaders(extra: Record<string, string> = {}) {
  return {
    [WEB_CLIENT_HEADER_NAME]: WEB_CLIENT_HEADER_VALUE,
    [TRANSFER_CLIENT_HEADER_NAME]: getTransferClientId(),
    Accept: 'application/json',
    ...extra,
  }
}

async function createUploadSession(file: File, fingerprint: string, signal?: AbortSignal) {
  const csrfToken = await getCsrfToken()
  throwIfCanceled(signal)
  const response = await fetch('/api/transfer/uploads', {
    method: 'POST',
    credentials: 'same-origin',
    headers: webHeaders({
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    }),
    body: JSON.stringify({
      filename: file.name,
      total_size: file.size,
      fingerprint,
    }),
    signal,
  })
  return parseResponse<TransferUploadSession>(response)
}

async function getUploadSession(uploadId: string, signal?: AbortSignal) {
  const response = await fetch(`/api/transfer/uploads/${encodeURIComponent(uploadId)}`, {
    credentials: 'same-origin',
    headers: webHeaders(),
    signal,
  })
  return parseResponse<TransferUploadSession>(response)
}

function chunkErrorMessage(response: unknown, status: number) {
  if (response && typeof response === 'object' && 'error' in response) {
    return String((response as { error?: unknown }).error || `Request failed with ${status}`)
  }
  return `Request failed with ${status}`
}

function uploadChunk(
  uploadId: string,
  chunk: Blob,
  offset: number,
  totalSize: number,
  csrfToken: string,
  options: TransferUploadOptions,
) {
  return new Promise<TransferUploadSession>((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    const abortUpload = () => xhr.abort()
    const cleanup = () => options.signal?.removeEventListener('abort', abortUpload)
    xhr.open('PATCH', `/api/transfer/uploads/${encodeURIComponent(uploadId)}`)
    xhr.withCredentials = true
    xhr.responseType = 'json'
    xhr.setRequestHeader(WEB_CLIENT_HEADER_NAME, WEB_CLIENT_HEADER_VALUE)
    xhr.setRequestHeader(TRANSFER_CLIENT_HEADER_NAME, getTransferClientId())
    xhr.setRequestHeader('Accept', 'application/json')
    xhr.setRequestHeader('Content-Type', 'application/offset+octet-stream')
    xhr.setRequestHeader('Upload-Offset', String(offset))
    xhr.setRequestHeader('X-CSRF-Token', csrfToken)
    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable) return
      const loaded = Math.min(totalSize, offset + event.loaded)
      options.onProgress?.({
        loaded,
        total: totalSize,
        percent: totalSize > 0 ? (loaded / totalSize) * 100 : 100,
      })
    }
    xhr.onload = () => {
      cleanup()
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(xhr.response as TransferUploadSession)
        return
      }
      if (xhr.status === 401) {
        redirectToLogin()
        reject(new AuthRequiredError())
        return
      }
      reject(new TransferChunkRequestError(chunkErrorMessage(xhr.response, xhr.status), xhr.status))
    }
    xhr.onerror = () => {
      cleanup()
      reject(new TransferChunkNetworkError())
    }
    xhr.onabort = () => {
      cleanup()
      reject(new UploadCanceledError())
    }
    options.signal?.addEventListener('abort', abortUpload, { once: true })
    xhr.send(chunk)
  })
}

function waitBeforeRetry(delayMs: number, signal?: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    throwIfCanceled(signal)
    const timer = window.setTimeout(() => {
      signal?.removeEventListener('abort', cancelWait)
      resolve()
    }, delayMs)
    const cancelWait = () => {
      window.clearTimeout(timer)
      reject(new UploadCanceledError())
    }
    signal?.addEventListener('abort', cancelWait, { once: true })
  })
}

function completedResult(session: TransferUploadSession): TransferUploadResult {
  return {
    name: session.name,
    bytes_written: session.total_size,
    replaced: session.replaced,
    download_url: session.download_url,
    share_url: session.share_url,
    expires_at: session.expires_at,
    temporary: session.temporary,
  }
}

export async function uploadTransferFile(file: File, options: TransferUploadOptions = {}) {
  throwIfCanceled(options.signal)
  const fingerprint = await fileFingerprint(file, options.signal)
  let session = await createUploadSession(file, fingerprint, options.signal)
  let offset = session.offset
  if (offset > 0 && !session.complete) options.onResume?.(offset)
  options.onProgress?.({
    loaded: offset,
    total: file.size,
    percent: file.size > 0 ? (offset / file.size) * 100 : 100,
  })
  if (session.complete) return completedResult(session)

  const csrfToken = await getCsrfToken()
  const chunkSize = Math.max(1, session.chunk_size || DEFAULT_CHUNK_SIZE)
  while (offset < file.size) {
    throwIfCanceled(options.signal)
    const end = Math.min(file.size, offset + chunkSize)
    const chunk = file.slice(offset, end)
    let retryCount = 0

    while (true) {
      try {
        session = await uploadChunk(session.upload_id, chunk, offset, file.size, csrfToken, options)
        offset = session.offset
        options.onProgress?.({
          loaded: offset,
          total: file.size,
          percent: file.size > 0 ? (offset / file.size) * 100 : 100,
        })
        break
      } catch (error) {
        if (error instanceof UploadCanceledError || error instanceof AuthRequiredError) throw error
        if (error instanceof TransferChunkRequestError && error.status < 500 && error.status !== 409) throw error

        try {
          const recovered = await getUploadSession(session.upload_id, options.signal)
          if (recovered.complete) return completedResult(recovered)
          if (recovered.offset !== offset) {
            session = recovered
            offset = recovered.offset
            options.onResume?.(offset)
            options.onProgress?.({
              loaded: offset,
              total: file.size,
              percent: file.size > 0 ? (offset / file.size) * 100 : 100,
            })
            break
          }
        } catch (statusError) {
          if (statusError instanceof UploadCanceledError || statusError instanceof AuthRequiredError) throw statusError
        }

        retryCount += 1
        if (retryCount >= MAX_CHUNK_RETRIES) {
          throw new Error('网络连接持续中断，已保存上传断点；请稍后点击重试继续', { cause: error })
        }
        await waitBeforeRetry(500 * 2 ** (retryCount - 1), options.signal)
      }
    }
  }

  if (!session.complete) session = await getUploadSession(session.upload_id, options.signal)
  if (!session.complete) throw new Error('服务器尚未确认上传完成，断点已保留')
  options.onProgress?.({ loaded: file.size, total: file.size, percent: 100 })
  return completedResult(session)
}
