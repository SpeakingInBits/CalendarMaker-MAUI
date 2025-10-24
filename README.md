# CalendarMaker-MAUI

CalendarMaker-MAUI is a cross-platform desktop application built with .NET MAUI that allows users to create beautiful, 
customizable photo calendars. Design professional monthly calendars with your photos, customize layouts, and export high-quality PDFs ready for printing.

## Version 0.4 Features

### Calendar Creation

- **Monthly Calendar Generation**: Create calendars with 12 customizable monthly pages
- **Flexible Start Month**: Begin your calendar from any month of the year
- **First Day of Week**: Choose between Sunday or Monday as the first day
- **Year Selection**: Create calendars for any year (1900-2100)
- **Double-Sided Calendars**: Yearly calendars that will print front and back (current version always starts in January and also shows the previous year December)

### Photo Management

- **Multiple Photo Layouts**: Choose from 6 different photo arrangements per page:
  - Single photo (full photo area)
  - Two photos (vertical split)
  - Four photos (2×2 grid)
  - Two photos (horizontal stack)
  - Three photos (2 left, 1 right)
  - Three photos (1 left, 2 right)
- **Cover Pages**: Front and back cover support with multi-photo layouts
- **Photo Slots**: Assign different photos to each slot in multi-photo layouts
- **Pan & Zoom**: Adjust photo positioning and zoom level (0.5× to 3×) independently for each photo
- **Smart Photo Management**: Reuse photos across multiple months and slots
- **Photo Import**: Easily import multiple photos at once

### Layout Customization

- **Flexible Page Layouts**: Four layout configurations:
  - Photo Top / Calendar Bottom
  - Photo Bottom / Calendar Top
  - Photo Left / Calendar Right
  - Photo Right / Calendar Left
- **Adjustable Split Ratio**: Control the space allocation between photos and calendar (30%-70%)
- **Visual Designer**: Interactive canvas with live preview of your calendar pages
- **Page Navigation**: Easily navigate between cover pages and all 12 months

### Page Sizes & Export

- **Multiple Page Sizes**:
  - 5×7 inches
  - Letter (8.5×11 inches)
  - Tabloid/Ledger (11×17 inches)
  - Super B (13×19 inches)
- **High-Quality PDF Export**: Export at 300 DPI for professional printing
- **Flexible Export Options**:
  - Export individual months
  - Export front cover only
  - Export back cover only
  - Export complete year (all pages)
- **Progress Tracking**: Visual progress indicator for multi-page exports
- **Parallel Rendering**: Fast export using optimized parallel processing

### Project Management

- **Multiple Projects**: Create and manage multiple calendar projects
- **Project Persistence**: All projects saved locally using SQLite.
- **Asset Management**: Photos organized per-project with efficient storage.

### Advanced Features

- **Slot-Based Photo Assignment**: Assign different photos to specific slots in multi-photo layouts
- **Independent Photo Controls**: Each photo slot has its own pan and zoom settings
- **Interactive Slot Selection**: Click to select active slot, double-click to assign photos
- **Visual Slot Indicators**: Blue borders highlight the currently selected slot
- **Keyboard Shortcuts** (Windows): Arrow keys to adjust pan position
- **Responsive UI**: Smooth canvas rendering with touch and mouse support

### Privacy First

- **Completely Offline**: No internet connection required
- **Local Storage**: All data and photos stored locally on your device. 100% privacy.
- **No Telemetry**: Your privacy is respected - no data collection

## Getting Started

### Prerequisites

To build and run CalendarMaker-MAUI, you'll need:

1. **.NET 10 SDK**
   - Download and install from [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
   - Verify installation: `dotnet --version` (should show 10.0.x)

2. **Visual Studio 2022** (17.13 or later)
   - Download from [https://visualstudio.microsoft.com/](https://visualstudio.microsoft.com/)
   - Required for Windows development

3. **.NET MAUI Workload**
   - Install via Visual Studio Installer:
     - Open Visual Studio Installer
     - Click "Modify" on your Visual Studio installation
     - Under "Workloads" tab, check ".NET Multi-platform App UI development"
     - Click "Modify" to install

   - Or install via command line:
     ```bash
     dotnet workload install maui
     ```

### Building the Project

1. **Clone the repository**
   ```bash
   git clone https://github.com/SpeakingInBits/CalendarMaker-MAUI.git
   cd CalendarMaker-MAUI
   ```

2. **Open in Visual Studio**
   - Open `CalendarMaker-MAUI.sln` in Visual Studio 2022
   - Build project to restore NuGet packages for NuGet packages

3. **Select target platform**
   - In the toolbar, select your target platform:
     - **Windows**: `net10.0-windows10.0.19041.0` (Only supported platform currently)
     - **Android**: `net10.0-android`
     - **iOS**: `net10.0-ios`
     - **Mac Catalyst**: `net10.0-maccatalyst`

4. **Build and run**
   - Press `F5` or click the "Start" button
     - Or build from command line:
     ```bash
     dotnet build
     dotnet run --framework net10.0-windows10.0.19041.0
     ```

### Platform-Specific Notes

- **Windows**: Fully supported and tested. Primary development platform.
- **Android**: Not tested yet; build should succeed but functionality may be limited.
- **iOS/Mac**: Not tested.

### Project Structure

```
CalendarMaker-MAUI/
├── CalendarMaker-MAUI/
│   ├── Models/          # Data models
│   ├── Services/        # Business logic and rendering
│   ├── Views/           # UI pages and components
│   ├── Resources/       # Images, fonts, styles
│   └── MauiProgram.cs   # App configuration and DI
├── README.md
```

## Dependencies

- **Microsoft.Maui.Controls**: Cross-platform UI framework
- **CommunityToolkit.Maui**: Additional MAUI controls and helpers
- **CommunityToolkit.Mvvm**: MVVM infrastructure
- **SkiaSharp.Views.Maui**: Canvas rendering for live preview
- **QuestPDF**: High-quality PDF generation

## Usage

1. **Create a New Project**
   - Launch the app and click "Create New Project"
   - Set your calendar year and start month
   - Choose page size and orientation
   - Select default layout preferences

2. **Add Photos**
   - Click "Add Photo" to import photos into your project
   - Navigate between months using Previous/Next buttons
   - Navigate to covers using the page navigation

3. **Customize Layouts**
   - Choose photo layout for each page (single, 2-photo, 4-photo, etc.)
   - Select active photo slot by clicking on it
   - Double-click a slot to assign a photo
   - Adjust split ratio between photo and calendar areas
   - Use zoom slider to adjust each photo independently

4. **Export Your Calendar**
   - Export individual months or covers
   - Export complete year with all pages
   - Choose export location
   - PDFs are generated at 300 DPI for professional printing

## Roadmap

Future enhancements planned:

- Event management (single and recurring events)
- US federal holidays overlay
- Additional photo layouts and collages
- Drag-and-drop photo import
- Project import/export as ZIP
- Custom fonts per project
- Theming options per project
- Possible multi-platform support (Mac, iOS, Android)
- WYSIWYG designer improvements

## Contributing

We welcome contributions! Whether it's bug reports, feature requests, or code contributions, your help is appreciated.

### How to Contribute

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Contributors

Thanks to all the amazing people who have contributed to this project!

<a href="https://github.com/SpeakingInBits/CalendarMaker-MAUI/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=SpeakingInBits/CalendarMaker-MAUI" />
</a>

Made with [contrib.rocks](https://contrib.rocks).