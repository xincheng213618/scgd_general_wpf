import type {
  BrowsePayload,
  CvwsContext,
  HomePayload,
  PluginDetail,
  PluginListResponse,
  ReleasesPayload,
  ToolsPayload,
  TransferFilesResponse,
  UpdatesPayload,
  UploadContext,
} from '../types/site'
import { AuthRequiredError, getJson, parseResponse, postForm } from './request'

function queryString(params: Record<string, string | number | undefined>) {
  const search = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && String(value).trim() !== '') {
      search.set(key, String(value))
    }
  })
  const text = search.toString()
  return text ? `?${text}` : ''
}

export function getHome() {
  return getJson<HomePayload>('/api/site/home')
}

export function getReleases(params: {
  major_minor?: string
  branch?: string
  kind?: string
  era?: string
}) {
  return getJson<ReleasesPayload>(`/api/site/releases${queryString(params)}`)
}

export function getChangelog() {
  return getJson<{ app_info: { latest_version?: string; changelog?: string; changelog_html?: string } }>(
    '/api/site/changelog',
  )
}

export function getUpdates() {
  return getJson<UpdatesPayload>('/api/site/updates')
}

export function getTools() {
  return getJson<ToolsPayload>('/api/site/tools')
}

export function getBrowse(subpath = '', params: { limit?: number; offset?: number } = {}) {
  const path = subpath ? `/${subpath.split('/').map(encodeURIComponent).join('/')}` : ''
  return getJson<BrowsePayload>(`/api/site/browse${path}${queryString(params)}`)
}

export function getUploadContext() {
  return getJson<UploadContext>('/api/site/upload/context')
}

export function getPlugins(params: {
  keyword?: string
  category?: string
  author?: string
  sort?: string
  sortOrder?: string
  page?: number
  pageSize?: number
}) {
  return getJson<PluginListResponse>(
    `/api/plugins${queryString({
      Keyword: params.keyword,
      Category: params.category,
      Author: params.author,
      SortBy: params.sort,
      SortOrder: params.sortOrder,
      Page: params.page,
      PageSize: params.pageSize,
    })}`,
  )
}

export function getPluginCategories() {
  return getJson<string[]>('/api/plugins/categories')
}

export function getPluginDetail(pluginId: string) {
  return getJson<PluginDetail>(`/api/plugins/${encodeURIComponent(pluginId)}`)
}

export function publishPluginPackage(formData: FormData) {
  return postForm<{ pluginId: string; version: string }>('/api/packages/publish', formData)
}

export function getTransferFiles() {
  return getJson<TransferFilesResponse>('/api/transfer/files')
}

export function deleteTransferFile(name: string) {
  return fetch(`/api/transfer/files/${encodeURIComponent(name)}`, {
    method: 'DELETE',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
  }).then((response) => parseResponse<{ deleted: string }>(response))
}

export function uploadTransferFile(file: File, onProgress?: (percent: number) => void) {
  return new Promise<{ name: string; bytes_written: number; replaced: boolean; download_url: string }>(
    (resolve, reject) => {
      const xhr = new XMLHttpRequest()
      xhr.open('PUT', `/api/transfer/files/${encodeURIComponent(file.name)}`)
      xhr.withCredentials = true
      xhr.responseType = 'json'
      xhr.setRequestHeader('Accept', 'application/json')
      xhr.setRequestHeader('Content-Type', 'application/octet-stream')
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable && onProgress) {
          onProgress((event.loaded / event.total) * 100)
        }
      }
      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve(xhr.response)
          return
        }
        if (xhr.status === 401) {
          reject(new AuthRequiredError())
          return
        }
        reject(new Error(xhr.response?.error || `Request failed with ${xhr.status}`))
      }
      xhr.onerror = () => reject(new Error('上传失败'))
      xhr.send(file)
    },
  )
}

export function getCvwsContext() {
  return getJson<CvwsContext>('/api/tool/cvwindowsservice/context')
}

export function publishCvwsPackage(formData: FormData) {
  return postForm('/api/tool/cvwindowsservice/publish', formData)
}
