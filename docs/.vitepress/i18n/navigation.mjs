import { createRequire } from 'node:module'
import { getLocalizedText, toLocalePath } from './locales.mjs'

const require = createRequire(import.meta.url)
const { navItems, sidebarItems } = require('./navigation-data.json')

function localizeItems(localeKey, items) {
  return items.map((item) => {
    const localizedItem = {
      text: getLocalizedText(item.text, localeKey),
    }

    if ('collapsed' in item) {
      localizedItem.collapsed = item.collapsed
    }

    if ('link' in item) {
      localizedItem.link = toLocalePath(localeKey, item.link)
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