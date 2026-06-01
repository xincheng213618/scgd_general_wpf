import { createRequire } from 'node:module'

// Shared locale helpers for the docs site and generated search artifacts.
// Locale definitions live in locale-definitions.json so scaffold scripts can
// register new locales without hand-editing config code.

const require = createRequire(import.meta.url)

export const defaultLocaleKey = 'root'
export const defaultSectionKey = 'root'
export const editLinkPattern = 'https://github.com/xincheng213618/scgd_general_wpf/edit/master/docs/:path'
export const footerCopyright = 'Copyright © 2025-present ColorVision Development Team'
export const lastUpdatedFormatOptions = {
  dateStyle: 'short',
  timeStyle: 'short',
}

export const localeDefinitions = require('./locale-definitions.json')

const sectionBlueprints = [
  { key: defaultSectionKey, title: { root: '首页与入口', en: 'Home & Entry' }, link: '/' },
  { key: '00-getting-started', title: { root: '安装与首次使用', en: 'Getting Started' }, link: '/00-getting-started/README' },
  { key: '01-user-guide', title: { root: '日常使用', en: 'Daily Use' }, link: '/01-user-guide/README' },
  { key: '02-developer-guide', title: { root: '开发与交付', en: 'Development & Delivery' }, link: '/02-developer-guide/README' },
  { key: '03-architecture', title: { root: '设计与架构', en: 'Design & Architecture' }, link: '/03-architecture/README' },
  { key: '04-api-reference', title: { root: 'API 参考', en: 'API Reference' }, link: '/04-api-reference/README' },
  { key: '05-resources', title: { root: '附录与资源', en: 'Resources & Appendix' }, link: '/05-resources/README' },
]

export const localeOrder = Object.keys(localeDefinitions)
export const sectionKeyOrder = sectionBlueprints.map((section) => section.key)

const sectionKeySet = new Set(sectionKeyOrder.filter((sectionKey) => sectionKey !== defaultSectionKey))

export function isLocaleKey(value) {
  return Object.prototype.hasOwnProperty.call(localeDefinitions, value)
}

export function getLocaleDefinition(localeKey = defaultLocaleKey) {
  return localeDefinitions[localeKey] ?? localeDefinitions[defaultLocaleKey]
}

export function getLocalizedText(localizedText, localeKey = defaultLocaleKey) {
  if (!localizedText) {
    return ''
  }

  return localizedText[localeKey] ?? localizedText[defaultLocaleKey] ?? Object.values(localizedText)[0] ?? ''
}

export function getLocaleHomeUrl(localeKey = defaultLocaleKey) {
  const pathPrefix = getLocaleDefinition(localeKey).pathPrefix
  return pathPrefix ? `/${pathPrefix}/` : '/'
}

export function toLocalePath(localeKey = defaultLocaleKey, link) {
  if (!link || !link.startsWith('/')) {
    return link
  }

  if (link.startsWith('/images/') || link.startsWith('/favicon')) {
    return link
  }

  const pathPrefix = getLocaleDefinition(localeKey).pathPrefix
  if (!pathPrefix) {
    return link
  }

  return link === '/' ? `/${pathPrefix}/` : `/${pathPrefix}${link}`
}

export function buildVitePressLocales(themeConfigFactory) {
  return Object.fromEntries(
    localeOrder.map((localeKey) => {
      const locale = getLocaleDefinition(localeKey)
      return [
        localeKey,
        {
          label: locale.label,
          lang: locale.lang,
          title: locale.title,
          description: locale.description,
          link: getLocaleHomeUrl(localeKey),
          themeConfig: themeConfigFactory(localeKey, locale),
        },
      ]
    }),
  )
}

export function buildSearchTranslations() {
  return Object.fromEntries(
    localeOrder.map((localeKey) => [localeKey, { translations: getLocaleDefinition(localeKey).searchTranslations }]),
  )
}

export function getSectionDefinitions(localeKey = defaultLocaleKey) {
  return sectionBlueprints.map((section) => ({
    key: section.key,
    title: getLocalizedText(section.title, localeKey),
    url: section.key === defaultSectionKey ? getLocaleHomeUrl(localeKey) : toLocalePath(localeKey, section.link),
  }))
}

export function getSectionDefinition(localeKey = defaultLocaleKey, sectionKey) {
  return getSectionDefinitions(localeKey).find((section) => section.key === sectionKey)
}

export function getSectionTitle(localeKey = defaultLocaleKey, sectionKey) {
  return getSectionDefinition(localeKey, sectionKey)?.title ?? sectionKey
}

export function getSectionUrl(localeKey = defaultLocaleKey, sectionKey) {
  return getSectionDefinition(localeKey, sectionKey)?.url ?? getLocaleHomeUrl(localeKey)
}

export function getSectionSortIndex(localeKey = defaultLocaleKey, sectionKey) {
  const index = getSectionDefinitions(localeKey).findIndex((section) => section.key === sectionKey)
  return index === -1 ? Number.MAX_SAFE_INTEGER : index
}

export function isKnownSectionKey(sectionKey) {
  return sectionKeySet.has(sectionKey)
}