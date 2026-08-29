import type { TransferShare } from '../types/site'
import { getJson } from './request'

export function getTransferShare(token: string, signal?: AbortSignal) {
  return getJson<TransferShare>(`/api/transfer/shares/${encodeURIComponent(token)}`, signal)
}
