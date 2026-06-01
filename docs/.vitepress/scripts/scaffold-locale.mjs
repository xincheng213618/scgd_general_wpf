import fs from 'node:fs/promises'
import path from 'node:path'
import {
  defaultLocaleKey,
  getLocaleDefinition,
  isLocaleKey,
  localeDefinitions,
  toLocalePath,
} from '../i18n/locales.mjs'

const docsRoot = path.resolve(process.cwd(), 'docs')
const localeDefinitionsPath = path.join(docsRoot, '.vitepress', 'i18n', 'locale-definitions.json')
const navigationDataPath = path.join(docsRoot, '.vitepress', 'i18n', 'navigation-data.json')
const localeDirectoryPrefixes = new Set(
  Object.values(localeDefinitions)
    .map((definition) => definition.pathPrefix)
    .filter(Boolean),
)
const rootContentExclusions = new Set(['.vitepress', 'assets', 'public'])
const externalRootExclusions = new Set(['.vitepress', 'assets', 'public', 'translation_index.json'])

async function main() {
  const options = parseArgs(process.argv.slice(2))

  if (options.help) {
    printUsage()
    return
  }

  await validateOptions(options)

  const sourceLocaleKey = options.source ?? defaultLocaleKey
  const targetPathPrefix = options.pathPrefix ?? options.locale
  const sourceRoot = options.sourceDir ? path.resolve(process.cwd(), options.sourceDir) : getLocaleRoot(sourceLocaleKey)
  const sourceDefinition = getLocaleDefinition(sourceLocaleKey)
  const targetRoot = path.join(docsRoot, targetPathPrefix)
  const copyEntries = await collectCopyEntries({ sourceLocaleKey, sourceRoot, sourceDir: options.sourceDir })
  const currentNavigationData = await readJson(navigationDataPath)
  const nextDefinitions = buildNextLocaleDefinitions({
    localeKey: options.locale,
    targetPathPrefix,
    label: options.label,
    lang: options.lang,
    sourceDefinition,
  })
  const { navigationData: nextNavigationData, localizedLabelCount } = buildNextNavigationData({
    navigationData: currentNavigationData,
    localeKey: options.locale,
    sourceLocaleKey,
  })

  if (options.dryRun) {
    printPlan({
      localeKey: options.locale,
      sourceLocaleKey,
      sourceRoot,
      targetPathPrefix,
      targetRoot,
      copyEntries,
      nextDefinitions,
      localizedLabelCount,
    })
    return
  }

  if (!options.force && await pathExists(targetRoot)) {
    throw new Error(`Target docs directory already exists: ${targetRoot}`)
  }

  await copyLocaleContent({
    copyEntries,
    localeKey: options.locale,
    targetPathPrefix,
    sourceRoot,
    targetRoot,
    force: options.force,
  })
  await Promise.all([
    writeLocaleDefinitions(nextDefinitions),
    writeNavigationData(nextNavigationData),
  ])

  console.log(`Registered locale '${options.locale}' at docs/${targetPathPrefix}`)
  console.log(`Seeded ${localizedLabelCount} navigation labels in docs/.vitepress/i18n/navigation-data.json`)
  console.log('Next steps: translate the copied Markdown files, then review the seeded menu labels in docs/.vitepress/i18n/navigation-data.json.')
}

function parseArgs(args) {
  const options = {
    dryRun: false,
    force: false,
    help: false,
    label: undefined,
    lang: undefined,
    locale: undefined,
    pathPrefix: undefined,
    source: defaultLocaleKey,
    sourceDir: undefined,
  }

  for (let index = 0; index < args.length; index += 1) {
    const token = args[index]

    if (token === '--dry-run') {
      options.dryRun = true
      continue
    }

    if (token === '--force') {
      options.force = true
      continue
    }

    if (token === '--help' || token === '-h') {
      options.help = true
      continue
    }

    if (token.startsWith('--')) {
      const nextValue = args[index + 1]
      if (!nextValue || nextValue.startsWith('--')) {
        throw new Error(`Missing value for ${token}`)
      }

      switch (token) {
        case '--locale':
          options.locale = nextValue
          break
        case '--label':
          options.label = nextValue
          break
        case '--lang':
          options.lang = nextValue
          break
        case '--path-prefix':
          options.pathPrefix = nextValue
          break
        case '--source':
          options.source = nextValue
          break
        case '--source-dir':
          options.sourceDir = nextValue
          break
        default:
          throw new Error(`Unknown option: ${token}`)
      }

      index += 1
      continue
    }

    if (!options.locale) {
      options.locale = token
      continue
    }

    throw new Error(`Unexpected argument: ${token}`)
  }

  return options
}

async function validateOptions(options) {
  if (!options.locale) {
    throw new Error('Locale key is required. Example: --locale ja')
  }

  if (options.locale === defaultLocaleKey) {
    throw new Error(`Locale key '${defaultLocaleKey}' is reserved for the default locale`)
  }

  if (!isSafeSegment(options.locale)) {
    throw new Error(`Locale key '${options.locale}' must use lowercase letters, numbers, or dashes`)
  }

  const sourceLocaleKey = options.source ?? defaultLocaleKey
  if (!isLocaleKey(sourceLocaleKey)) {
    throw new Error(`Unknown source locale: ${sourceLocaleKey}`)
  }

  if (options.sourceDir) {
    const resolvedSourceDir = path.resolve(process.cwd(), options.sourceDir)
    if (!await pathExists(resolvedSourceDir)) {
      throw new Error(`Source directory does not exist: ${resolvedSourceDir}`)
    }
  }

  const targetPathPrefix = options.pathPrefix ?? options.locale
  if (!isSafeSegment(targetPathPrefix)) {
    throw new Error(`Path prefix '${targetPathPrefix}' must use lowercase letters, numbers, or dashes`)
  }

  if (!options.force && isLocaleKey(options.locale)) {
    throw new Error(`Locale '${options.locale}' already exists in locale-definitions.json`)
  }

  const conflictingLocale = Object.entries(localeDefinitions).find(([localeKey, definition]) => {
    return localeKey !== options.locale && definition.pathPrefix === targetPathPrefix
  })

  if (conflictingLocale) {
    throw new Error(`Path prefix '${targetPathPrefix}' is already used by locale '${conflictingLocale[0]}'`)
  }
}

function buildNextLocaleDefinitions({ localeKey, targetPathPrefix, label, lang, sourceDefinition }) {
  return {
    ...localeDefinitions,
    [localeKey]: {
      ...sourceDefinition,
      label: label ?? localeKey,
      lang: lang ?? localeKey,
      pathPrefix: targetPathPrefix,
    },
  }
}

function buildNextNavigationData({ navigationData, localeKey, sourceLocaleKey }) {
  let localizedLabelCount = 0

  function localizeNode(node) {
    if (Array.isArray(node)) {
      return node.map(localizeNode)
    }

    if (!node || typeof node !== 'object') {
      return node
    }

    const localizedNode = {}

    for (const [key, value] of Object.entries(node)) {
      if (key === 'text' && value && typeof value === 'object') {
        const localizedText = { ...value }
        localizedText[localeKey] = value[sourceLocaleKey] ?? value[defaultLocaleKey] ?? Object.values(value)[0] ?? ''
        localizedNode[key] = localizedText
        localizedLabelCount += 1
        continue
      }

      localizedNode[key] = localizeNode(value)
    }

    return localizedNode
  }

  return {
    navigationData: localizeNode(navigationData),
    localizedLabelCount,
  }
}

function getLocaleRoot(localeKey) {
  if (localeKey === defaultLocaleKey) {
    return docsRoot
  }

  return path.join(docsRoot, getLocaleDefinition(localeKey).pathPrefix)
}

async function collectCopyEntries({ sourceLocaleKey, sourceRoot, sourceDir }) {
  if (sourceDir) {
    return collectExternalCopyEntries(sourceRoot)
  }

  if (sourceLocaleKey === defaultLocaleKey) {
    const entries = await fs.readdir(sourceRoot, { withFileTypes: true })
    const rootEntries = []

    for (const entry of entries) {
      if (shouldSkipRootEntry(entry.name)) {
        continue
      }

      const fullPath = path.join(sourceRoot, entry.name)
      if (entry.isDirectory()) {
        rootEntries.push(...await collectFilesRecursive(fullPath))
        continue
      }

      if (entry.isFile() && entry.name.toLowerCase().endsWith('.md')) {
        rootEntries.push(fullPath)
      }
    }

    return rootEntries
  }

  return collectFilesRecursive(sourceRoot)
}

async function collectExternalCopyEntries(sourceRoot) {
  const entries = await fs.readdir(sourceRoot, { withFileTypes: true })
  const sourceEntries = []

  for (const entry of entries) {
    if (shouldSkipExternalRootEntry(entry.name)) {
      continue
    }

    const fullPath = path.join(sourceRoot, entry.name)
    if (entry.isDirectory()) {
      sourceEntries.push(...await collectFilesRecursive(fullPath))
      continue
    }

    if (entry.isFile()) {
      sourceEntries.push(fullPath)
    }
  }

  return sourceEntries
}

function shouldSkipRootEntry(entryName) {
  return rootContentExclusions.has(entryName) || localeDirectoryPrefixes.has(entryName)
}

function shouldSkipExternalRootEntry(entryName) {
  return externalRootExclusions.has(entryName) || localeDirectoryPrefixes.has(entryName)
}

async function collectFilesRecursive(rootDirectory) {
  const entries = await fs.readdir(rootDirectory, { withFileTypes: true })
  const files = []

  for (const entry of entries) {
    const fullPath = path.join(rootDirectory, entry.name)
    if (entry.isDirectory()) {
      files.push(...await collectFilesRecursive(fullPath))
      continue
    }

    if (entry.isFile()) {
      files.push(fullPath)
    }
  }

  return files
}

async function copyLocaleContent({ copyEntries, localeKey, targetPathPrefix, sourceRoot, targetRoot, force }) {
  await fs.mkdir(targetRoot, { recursive: true })

  for (const sourcePath of copyEntries) {
    const relativePath = path.relative(sourceRoot, sourcePath)
    const targetPath = path.join(targetRoot, relativePath)

    if (!force && await pathExists(targetPath)) {
      throw new Error(`Target file already exists: ${targetPath}`)
    }

    await fs.mkdir(path.dirname(targetPath), { recursive: true })

    if (sourcePath.toLowerCase().endsWith('.md')) {
      const sourceContent = await fs.readFile(sourcePath, 'utf8')
      const localizedContent = rewriteLocalizedLinks(sourceContent, localeKey, targetPathPrefix)
      await fs.writeFile(targetPath, localizedContent, 'utf8')
      continue
    }

    await fs.copyFile(sourcePath, targetPath)
  }
}

function rewriteLocalizedLinks(content, localeKey, targetPathPrefix) {
  let localizedContent = content

  localizedContent = localizedContent.replace(/(!?\[[^\]]*\]\()([^\s)]+)([^)]*\))/g, (match, prefix, url, suffix) => {
    if (!url.startsWith('/')) {
      return match
    }

    return `${prefix}${toTargetLocalePath(localeKey, targetPathPrefix, url)}${suffix}`
  })

  localizedContent = localizedContent.replace(/^(\s*\[[^\]]+\]:\s*)(\/\S+)/gm, (match, prefix, url) => {
    return `${prefix}${toTargetLocalePath(localeKey, targetPathPrefix, url)}`
  })

  localizedContent = localizedContent.replace(/((?:href|src)=["'])(\/[^"']+)(["'])/g, (match, prefix, url, suffix) => {
    return `${prefix}${toTargetLocalePath(localeKey, targetPathPrefix, url)}${suffix}`
  })

  localizedContent = localizedContent.replace(/^(\s*link:\s*)(\/\S+)(\s*)$/gm, (match, prefix, url, suffix) => {
    return `${prefix}${toTargetLocalePath(localeKey, targetPathPrefix, url)}${suffix}`
  })

  return localizedContent
}

function toTargetLocalePath(localeKey, targetPathPrefix, link) {
  if (isLocaleKey(localeKey)) {
    return toLocalePath(localeKey, link)
  }

  if (!link || !link.startsWith('/')) {
    return link
  }

  if (link.startsWith('/images/') || link.startsWith('/favicon')) {
    return link
  }

  if (!targetPathPrefix) {
    return link
  }

  return link === '/' ? `/${targetPathPrefix}/` : `/${targetPathPrefix}${link}`
}

async function writeLocaleDefinitions(nextDefinitions) {
  await fs.writeFile(localeDefinitionsPath, `${JSON.stringify(nextDefinitions, null, 2)}\n`, 'utf8')
}

async function writeNavigationData(navigationData) {
  await fs.writeFile(navigationDataPath, `${JSON.stringify(navigationData, null, 2)}\n`, 'utf8')
}

function printPlan({ localeKey, sourceLocaleKey, sourceRoot, targetPathPrefix, targetRoot, copyEntries, nextDefinitions, localizedLabelCount }) {
  const nextDefinition = nextDefinitions[localeKey]
  console.log(`Dry run for locale '${localeKey}'`)
  console.log(`  Source locale: ${sourceLocaleKey}`)
  console.log(`  Source directory: ${sourceRoot}`)
  console.log(`  Target directory: ${targetRoot}`)
  console.log(`  Path prefix: ${targetPathPrefix}`)
  console.log(`  Label: ${nextDefinition.label}`)
  console.log(`  Lang: ${nextDefinition.lang}`)
  console.log(`  Files to copy: ${copyEntries.length}`)
  console.log(`  Navigation labels to seed: ${localizedLabelCount}`)
}

function printUsage() {
  console.log('Usage: node docs/.vitepress/scripts/scaffold-locale.mjs --locale <key> [options]')
  console.log('')
  console.log('Options:')
  console.log('  --locale <key>        Locale key used in locale-definitions.json')
  console.log('  --label <text>        Display label in the language switcher')
  console.log('  --lang <code>         HTML lang attribute, for example ja-JP')
  console.log('  --path-prefix <dir>   Directory and URL prefix under docs/')
  console.log('  --source <locale>     Source locale to copy from. Defaults to root')
  console.log('  --source-dir <path>   External docs directory to import from instead of docs/<locale>')
  console.log('  --dry-run             Print the plan without writing files')
  console.log('  --force               Overwrite an existing target locale')
}

async function pathExists(targetPath) {
  try {
    await fs.access(targetPath)
    return true
  } catch {
    return false
  }
}

async function readJson(filePath) {
  return JSON.parse(await fs.readFile(filePath, 'utf8'))
}

function isSafeSegment(value) {
  return /^[a-z0-9-]+$/.test(value)
}

main().catch((error) => {
  console.error(error.message)
  process.exitCode = 1
})