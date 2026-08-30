import { createRequire } from 'node:module'
import { getLocalizedText } from './locales.mjs'

const require = createRequire(import.meta.url)
// A checked-in projection of the same catalog used by offline AI retrieval.
// Do not maintain a second, role-based navigation tree by hand.
const {
  navItems,
  sidebarItems,
} = require('./navigation-data.json')

function localizeItems(localeKey, items) {
  return items.map((item) => {
    const localizedItem = {
      text: getLocalizedText(item.text, localeKey),
    }

    if ('collapsed' in item) {
      localizedItem.collapsed = item.collapsed
    }

    if ('rawLink' in item) {
      localizedItem.link = item.rawLink
    } else if ('link' in item) {
      localizedItem.link = item.link
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
