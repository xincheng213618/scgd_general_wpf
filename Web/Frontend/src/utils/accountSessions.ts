export function sessionClientLabel(userAgent: string): string {
  const browser = /Edg\//i.test(userAgent)
    ? 'Microsoft Edge'
    : /Firefox\//i.test(userAgent)
      ? 'Firefox'
      : /Chrome\//i.test(userAgent)
        ? 'Chrome'
        : /Safari\//i.test(userAgent)
          ? 'Safari'
          : ''
  const system = /Windows/i.test(userAgent)
    ? 'Windows'
    : /Android/i.test(userAgent)
      ? 'Android'
      : /iPhone|iPad|iPod/i.test(userAgent)
        ? 'iOS/iPadOS'
        : /Mac OS X/i.test(userAgent)
          ? 'macOS'
          : /Linux/i.test(userAgent)
            ? 'Linux'
            : ''
  return [browser, system].filter(Boolean).join(' · ') || '未知客户端'
}

export function sessionAddressLabel(ipAddress: string): string {
  return ipAddress.trim() || '未知地址'
}
