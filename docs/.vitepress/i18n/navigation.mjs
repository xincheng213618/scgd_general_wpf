import { createRequire } from 'node:module'
import fs from 'node:fs'
import path from 'node:path'
import { getLocalizedText, toLocalePath } from './locales.mjs'

const require = createRequire(import.meta.url)
const { navItems, sidebarItems } = require('./navigation-data.json')
const docsRoot = path.resolve(process.cwd(), 'docs')

function isStaticOrExternalLink(link) {
  return !link || !link.startsWith('/') || link.startsWith('/images/') || link.startsWith('/favicon')
}

function resolveMarkdownCandidate(localizedLink) {
  const normalizedLink = localizedLink.replace(/^\/+/, '').replace(/\/$/, '/README')
  return path.join(docsRoot, `${normalizedLink}.md`)
}

function toExistingLocalePath(localeKey, link) {
  if (isStaticOrExternalLink(link)) {
    return link
  }

  const localizedLink = toLocalePath(localeKey, link)
  if (localeKey === 'root' || fs.existsSync(resolveMarkdownCandidate(localizedLink))) {
    return localizedLink
  }

  return link
}

function localizeItems(localeKey, items) {
  return items.map((item) => {
    const localizedItem = {
      text: getLocalizedText(item.text, localeKey),
    }

    if ('collapsed' in item) {
      localizedItem.collapsed = item.collapsed
    }

    if ('link' in item) {
      localizedItem.link = toExistingLocalePath(localeKey, item.link)
    }

    if ('items' in item) {
      localizedItem.items = localizeItems(localeKey, item.items)
    }

    return localizedItem
  })
}

export function createNavItems(localeKey) {
  return localizeItems(localeKey, navItems)
}

export function createSidebarItems(localeKey) {
  return localizeItems(localeKey, sidebarItems)
}
