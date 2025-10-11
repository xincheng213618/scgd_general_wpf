# VitePress Documentation Migration

This project has been migrated from Docsify to VitePress for improved performance, better build-time optimization, and enhanced developer experience.

## What Changed

### Before (Docsify)
- Client-side rendered documentation
- Used `docs/index.html` as the entry point
- Configured via `window.$docsify` in `index.html`
- Sidebar defined in `docs/_sidebar.md`
- Cover page in `docs/_coverpage.md`

### After (VitePress)
- Static site generation (SSG) with client-side hydration
- Configuration in `docs/.vitepress/config.mts`
- Custom theme in `docs/.vitepress/theme/`
- Home page using VitePress hero layout in `docs/index.md`
- Built output in `docs/.vitepress/dist/`

## Getting Started

### Prerequisites
- Node.js 18+ and npm

### Installation
```bash
npm install
```

### Development
Start the development server with hot reload:
```bash
npm run docs:dev
```

Access the site at `http://localhost:3000/scgd_general_wpf/`

### Build
Build the static site for production:
```bash
npm run docs:build
```

### Preview
Preview the production build locally:
```bash
npm run docs:preview
```

## Features

### Maintained from Docsify
- ✅ Chinese language support
- ✅ Dark/light theme toggle
- ✅ Search functionality (now built-in local search)
- ✅ Syntax highlighting for C#, JSON, XML, PowerShell, Bash
- ✅ Mermaid diagram support
- ✅ Custom styling adapted to VitePress theme
- ✅ Full sidebar navigation structure
- ✅ Code copy functionality

### New VitePress Features
- ⚡ Lightning-fast static site generation
- 🎨 Improved default theme with better UX
- 📱 Better mobile responsiveness
- 🔍 Enhanced search with better indexing
- 🚀 Optimized bundle sizes
- ♿ Better accessibility
- 📦 Easier deployment via GitHub Actions

## File Structure

```
docs/
├── .vitepress/
│   ├── config.mts          # VitePress configuration
│   ├── theme/
│   │   ├── index.ts        # Theme entry
│   │   └── custom.css      # Custom styles
│   ├── dist/               # Build output (gitignored)
│   └── cache/              # Build cache (gitignored)
├── index.md                # Home page
├── getting-started/        # Documentation pages
├── architecture/
├── plugins/
└── ...

Old Docsify files (archived):
├── index.html.docsify      # Old Docsify entry point
├── _sidebar.md.docsify     # Old sidebar config  
└── _coverpage.md.docsify   # Old cover page
```

## Configuration

The main configuration file is `docs/.vitepress/config.mts`. Key sections:

- **themeConfig**: Navigation, sidebar, search, footer
- **markdown**: Syntax highlighting, line numbers
- **head**: Meta tags, favicon
- **base**: Base URL for GitHub Pages

## Deployment

The site is automatically deployed to GitHub Pages when changes are pushed to the `master` branch via GitHub Actions (`.github/workflows/pages.yml`).

## Troubleshooting

### Generic Type Syntax Issues
Some C# generic types like `List<T>` or `Dictionary<K,V>` in markdown need to be escaped as `List\<T\>` to avoid being treated as HTML tags.

### Dead Links
The build process checks for broken links. To ignore specific patterns, update the `ignoreDeadLinks` configuration in `config.mts`.

### Build Errors
- Clear the cache: `rm -rf docs/.vitepress/cache docs/.vitepress/dist`
- Reinstall dependencies: `rm -rf node_modules package-lock.json && npm install`
- Check for markdown syntax errors in the files mentioned in error messages

## Markdown Extensions

VitePress supports many markdown extensions out of the box:

- Emoji: `:tada:` → 🎉
- Table of contents: `[[toc]]`
- Custom containers: `::: tip`, `::: warning`, `::: danger`
- Code groups and line highlighting
- Math equations (when configured)

## Resources

- [VitePress Documentation](https://vitepress.dev/)
- [Migration Guide](https://vitepress.dev/guide/migration-from-vuepress)
- [Markdown Extensions](https://vitepress.dev/guide/markdown)
- [Theme Customization](https://vitepress.dev/guide/custom-theme)

## Need Help?

- Check [VitePress GitHub Issues](https://github.com/vuejs/vitepress/issues)
- Review [VitePress Examples](https://github.com/vuejs/vitepress/tree/main/examples)
- Consult the [official documentation](https://vitepress.dev/)
