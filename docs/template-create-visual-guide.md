# TemplateCreate Window Visual Comparison

## Before (Original Implementation)

```
┌──────────────────────────────────────────────┐
│  Create Template Window                      │
├──────────────────────────────────────────────┤
│  ○ 默认模板                                   │
│  ○ SensorTemplate1                           │
│  ○ MyCustomTemplate                          │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │ Template1                           ▼│   │
│  └──────────────────────────────────────┘   │
│                                              │
│  ┌────────────────────────────────────────┐ │
│  │  PropertyGrid (Template Properties)    │ │
│  │                                        │ │
│  │  Property1: Value1                     │ │
│  │  Property2: Value2                     │ │
│  │  ...                                   │ │
│  └────────────────────────────────────────┘ │
│                                              │
│          ┌──────────────┐                    │
│          │    Create    │                    │
│          └──────────────┘                    │
└──────────────────────────────────────────────┘
```

**Issues:**
- Plain text only
- No visual distinction
- Limited information
- Not engaging


## After (Enhanced Implementation)

```
┌────────────────────────────────────────────────────────────┐
│  Create Template Window                                    │
├────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ ✓ 📄         │  │   📄         │  │   📄         │    │
│  │  默认模板     │  │ Template1    │  │ Template2    │    │
│  │使用系统默认   │  │创建: 2024-01 │  │创建: 2024-02 │    │
│  └══════════════┘  └──────────────┘  └──────────────┘    │
│  ^Selected (2px border)  ^Unselected (1px border)        │
│                                                           │
│  ┌──────────────────────────────────────────────────┐    │
│  │ Template1                                       ▼│    │
│  └──────────────────────────────────────────────────┘    │
│                                                           │
│  ┌────────────────────────────────────────────────────┐  │
│  │  PropertyGrid (Template Properties)                │  │
│  │                                                    │  │
│  │  Property1: Value1                                 │  │
│  │  Property2: Value2                                 │  │
│  │  ...                                               │  │
│  └────────────────────────────────────────────────────┘  │
│                                                           │
│              ┌──────────────┐                             │
│              │    Create    │                             │
│              └──────────────┘                             │
└────────────────────────────────────────────────────────────┘
```

**Improvements:**
- Visual card-based design
- Icons for better recognition
- File metadata (creation date)
- Clear selection feedback (thick border)
- Professional appearance


## Detailed Card Structure

### Default Template Card (Selected)
```
╔═════════════════╗  ← 2px Primary Color Border
║       📄        ║  ← Document Icon (24px, Primary Color)
║                 ║
║   默认模板      ║  ← Bold Title (13px)
║                 ║
║ 使用系统默认模板 ║  ← Description (10px, gray)
╚═════════════════╝
```

### File Template Card (Unselected)
```
┌─────────────────┐  ← 1px Border
│       📄        │  ← Document Icon (24px, Primary Color)
│                 │
│  SensorTemp1    │  ← Bold Title (13px)
│                 │
│创建时间: 2024-01│  ← File Metadata (10px, gray)
└─────────────────┘
```

## Layout with Multiple Templates

```
┌──────────────────────────────────────────────────────────────┐
│  ScrollViewer (MaxHeight: 150px)                             │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ WrapPanel (Horizontal Orientation)                     │  │
│  │                                                        │  │
│  │  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐   │  │
│  │  │ Card │  │ Card │  │ Card │  │ Card │  │ Card │   │  │
│  │  │  1   │  │  2   │  │  3   │  │  4   │  │  5   │   │  │
│  │  └──────┘  └──────┘  └──────┘  └──────┘  └──────┘   │  │
│  │                                                        │  │
│  │  ┌──────┐  ┌──────┐  ┌──────┐                        │  │
│  │  │ Card │  │ Card │  │ Card │                        │  │
│  │  │  6   │  │  7   │  │  8   │                        │  │
│  │  └──────┘  └──────┘  └──────┘                        │  │
│  └────────────────────────────────────────────────────────┘  │
│                          ↕ Scrollbar                         │
└──────────────────────────────────────────────────────────────┘
```

Benefits:
- Auto-wrap when cards don't fit in width
- Vertical scrolling for many templates
- Consistent spacing (3px margins)
- Responsive to window size


## CSS/XAML Styling

### Border Styling
```
Normal State:
- BorderThickness: 1px
- BorderBrush: BorderBrush (theme-aware)
- Background: RegionBrush (theme-aware)
- CornerRadius: 4px
- Padding: 10,8,10,8

Selected State:
- BorderThickness: 2px
- BorderBrush: PrimaryBrush (theme accent color)
```

### Typography
```
Icon:
- FontFamily: Segoe MDL2 Assets
- Character: \uE8A5 (Document icon)
- FontSize: 24px
- Color: PrimaryBrush

Title:
- FontWeight: Bold
- FontSize: 13px
- Color: GlobalTextBrush
- TextAlignment: Center

Description:
- FontSize: 10px
- Color: ThirdlyTextBrush (lighter gray)
- TextAlignment: Center
- Margin-Top: 3px
```

## User Interaction Flow

1. **Window Opens**
   - Default template card is pre-selected (checked)
   - Has 2px primary border to indicate selection
   
2. **User Hovers** (Future Enhancement)
   - Could add hover effect for better feedback
   
3. **User Clicks Card**
   - Previous selection: border changes to 1px
   - New selection: border changes to 2px primary color
   - TemplateFile variable updates automatically
   
4. **User Creates Template**
   - Selected template is used as base
   - New template inherits properties

## Code Architecture

```
TemplateCreate.xaml.cs
│
├── CreateTemplateCard()
│   ├── Create RadioButton
│   ├── Apply Style (safe fallback)
│   ├── Create Border (card container)
│   ├── Create StackPanel (content layout)
│   ├── Add Icon TextBlock
│   ├── Add Title TextBlock
│   ├── Add Description TextBlock
│   ├── Wire up Checked/Unchecked events
│   └── Return complete card
│
└── Window_Initialized()
    ├── Setup folder paths
    ├── Create default template card
    ├── Loop through template files
    │   ├── Get file info
    │   ├── Create card for each
    │   └── Add to WrapPanel
    └── Continue with original logic
```

## Theme Support

All colors are referenced from Application.Current.Resources:
- `RegionBrush` - Card background
- `BorderBrush` - Normal border
- `PrimaryBrush` - Selected border & icon color
- `GlobalTextBrush` - Title text
- `ThirdlyTextBrush` - Description text

This ensures the cards automatically adapt to:
- Light/Dark themes
- Custom color schemes
- User preferences
