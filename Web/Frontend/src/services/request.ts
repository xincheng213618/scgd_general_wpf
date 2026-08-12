export class AuthRequiredError extends Error {
  constructor() {
    super('Authentication required')
    this.name = 'AuthRequiredError'
  }
}

let csrfToken = ''
let csrfTokenRequest: Promise<string> | null = null

function captureCsrfToken(payload: unknown) {
  if (payload && typeof payload === 'object' && 'csrf_token' in payload) {
    const token = String((payload as { csrf_token?: unknown }).csrf_token || '')
    if (token) csrfToken = token
  }
}

export async function getCsrfToken() {
  if (csrfToken) return csrfToken
  if (!csrfTokenRequest) {
    csrfTokenRequest = fetch('/api/auth/session', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    })
      .then(async (response) => {
        const payload = await response.json()
        if (!response.ok) throw new Error(`Request failed with ${response.status}`)
        captureCsrfToken(payload)
        if (!csrfToken) throw new Error('CSRF token unavailable')
        return csrfToken
      })
      .finally(() => {
        csrfTokenRequest = null
      })
  }
  return csrfTokenRequest
}

export async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 401) {
    throw new AuthRequiredError()
  }

  const contentType = response.headers.get('content-type') || ''
  const payload = contentType.includes('application/json')
    ? await response.json()
    : await response.text()
  captureCsrfToken(payload)

  if (!response.ok) {
    const message =
      typeof payload === 'object' && payload && 'error' in payload
        ? String((payload as { error?: unknown }).error)
        : `Request failed with ${response.status}`
    throw new Error(message)
  }

  return payload as T
}

export async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    signal,
  })
  return parseResponse<T>(response)
}

export async function postJson<T = unknown>(url: string, body?: unknown): Promise<T> {
  const token = await getCsrfToken()
  const response = await fetch(url, {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'X-CSRF-Token': token,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  return parseResponse<T>(response)
}

export async function putJson<T = unknown>(url: string, body?: unknown): Promise<T> {
  const token = await getCsrfToken()
  const response = await fetch(url, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'X-CSRF-Token': token,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  return parseResponse<T>(response)
}

export async function deleteJson<T = unknown>(url: string): Promise<T> {
  const token = await getCsrfToken()
  const response = await fetch(url, {
    method: 'DELETE',
    credentials: 'same-origin',
    headers: { Accept: 'application/json', 'X-CSRF-Token': token },
  })
  return parseResponse<T>(response)
}

export async function postForm<T = unknown>(url: string, formData: FormData): Promise<T> {
  const token = await getCsrfToken()
  const response = await fetch(url, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { Accept: 'application/json', 'X-CSRF-Token': token },
    body: formData,
  })
  return parseResponse<T>(response)
}
