# CalendarMaker-MAUI Refactoring Plan

## Executive Summary

This document outlines a comprehensive refactoring plan for the CalendarMaker-MAUI project to improve OOP principles, software architecture, and adhere to best practices. The plan is organized into phases with trackable tasks.

---

## ?? Goals

1. **Improve Separation of Concerns**: Decouple UI logic from business logic
2. **Enhance Testability**: Enable comprehensive unit testing
3. **Apply SOLID Principles**: Especially Single Responsibility and Dependency Inversion
4. **Reduce Code Duplication**: Extract common patterns
5. **Improve Maintainability**: Better organization and documentation

---

## ?? Current Architecture Analysis

### Strengths ?
- Good use of Dependency Injection
- Interface-based service layer
- Clear separation between Models, Views, ViewModels, and Services
- Proper use of async/await patterns
- Community Toolkit MVVM integration

### Issues Identified ??

#### 1. **God Class Anti-Pattern**
- **DesignerPage.xaml.cs** (1,200+ lines): Contains UI, rendering, gesture handling, state management, and export logic
- **RenderAndExport.cs** (600+ lines): Mixing rendering logic with export orchestration

#### 2. **Violates Single Responsibility Principle (SRP)**
- `DesignerPage` handles: Canvas rendering, touch gestures, photo selection, navigation, export, UI state
- `PdfExportService` handles: Page rendering, bitmap caching, parallel processing, calendar drawing

#### 3. **Tight Coupling**
- Direct SKCanvas manipulation in UI code
- Export service directly draws calendar grids (should delegate to rendering service)
- DesignerPage creates modal dialogs directly

#### 4. **Limited Testability**
- No unit tests exist
- Services have dependencies on file system and UI components
- Difficult to mock rendering operations
- Business logic mixed with UI code

#### 5. **Code Duplication**
- `ComputePhotoSlots()` duplicated in DesignerPage and PdfExportService
- `ComputeSplit()` duplicated
- `DrawBitmapWithPanZoom()` duplicated
- Calendar grid drawing logic duplicated

#### 6. **Missing Abstractions**
- No dedicated rendering service
- No gesture handling abstraction
- No navigation service
- No dialog service

#### 7. **Validation & Error Handling**
- Limited input validation
- Swallowing exceptions in storage service
- No centralized error handling

#### 8. **Resource Management**
- Image cache management spread across methods
- No clear disposal patterns for SKBitmap instances
- Potential memory leaks with parallel rendering

---

## ??? Refactoring Phases

### Phase 1: Foundation & Infrastructure ??
**Priority: HIGH | Estimated Effort: 3-5 days**

#### Tasks
- [ ] **Task 1.1**: Create `IDialogService` interface and implementation
  - Abstracts DisplayAlert, modal navigation
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 1.2**: Create `INavigationService` interface and implementation
  - Wraps Shell navigation
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 1.3**: Create `IFilePickerService` interface and implementation
  - Abstracts FilePicker and FileSaver
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 1.4**: Implement logging infrastructure
  - Add ILogger support
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 1.5**: Create centralized exception handling
  - Global error handler
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 2: Extract Rendering Logic ??
**Priority: HIGH | Estimated Effort: 5-7 days**

#### Tasks
- [ ] **Task 2.1**: Create `ICalendarRenderer` interface
  - `SKBitmap RenderCalendarGrid(CalendarGridSpec spec)`
  - `SKBitmap RenderPhotoSlot(PhotoSlotSpec spec)`
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 2.2**: Create `CalendarRenderer` implementation
  - Extract all drawing logic from DesignerPage and PdfExportService
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 2.3**: Create `ILayoutCalculator` service
  - `List<SKRect> ComputePhotoSlots(...)`
  - `(SKRect photo, SKRect cal) ComputeSplit(...)`
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 2.4**: Create `IImageProcessor` service
  - Handles pan/zoom calculations
  - Bitmap loading and caching
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 2.5**: Create specification objects
  - `CalendarGridSpec`, `PhotoSlotSpec`, `RenderContext`
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 3: Refactor DesignerPage ??
**Priority: HIGH | Estimated Effort: 7-10 days**

#### Tasks
- [ ] **Task 3.1**: Create `DesignerViewModel`
  - Move all state management from code-behind
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 3.2**: Extract `GestureHandler` class
  - Separate all touch/gesture logic
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 3.3**: Extract `CanvasManager` class
  - Manages canvas rendering coordination
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 3.4**: Create `PageNavigationManager` class
  - Handles page index logic, month calculations
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 3.5**: Create `PhotoAssignmentCoordinator` class
  - Manages photo-to-slot assignment workflow
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 3.6**: Reduce DesignerPage to < 300 lines
  - Only view initialization and bindings
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 4: Improve Service Layer ??
**Priority: MEDIUM | Estimated Effort: 4-6 days**

#### Tasks
- [ ] **Task 4.1**: Add validation to `ProjectStorageService`
  - Validate project data before save
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 4.2**: Implement proper error handling in storage
  - Don't swallow exceptions
  - Return Result<T> pattern
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 4.3**: Refactor `AssetService`
  - Separate file operations from business logic
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 4.4**: Extract `IImageCacheService`
  - Centralized bitmap caching with lifecycle management
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 4.5**: Refactor `PdfExportService`
  - Separate rendering from orchestration
  - Delegate to CalendarRenderer
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 5: Unit Testing Infrastructure ??
**Priority: HIGH | Estimated Effort: 8-12 days**

#### Tasks
- [ ] **Task 5.1**: Create test project
  - `CalendarMaker.Tests` with xUnit/NUnit
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.2**: Add Moq/NSubstitute for mocking
  - Configure DI for tests
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.3**: Create test fixtures and builders
  - `CalendarProjectBuilder`, `TestDataFactory`
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.4**: Write tests for `CalendarEngine`
  - Test month grid generation
  - Edge cases (leap years, different first days)
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.5**: Write tests for `LayoutCalculator`
  - Test all PhotoLayout variations
  - Test split calculations
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.6**: Write tests for `ImageProcessor`
  - Pan/zoom calculations
  - Bitmap scaling
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.7**: Write tests for `AssetService`
  - Mock file system
  - Test photo assignment logic
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.8**: Write tests for `ProjectStorageService`
  - Mock file system
  - Test CRUD operations
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.9**: Write tests for ViewModels
  - `DesignerViewModel`, `ProjectsViewModel`
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 5.10**: Achieve >80% code coverage
  - Use coverage tools
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 6: Apply SOLID Principles ???
**Priority: MEDIUM | Estimated Effort: 4-6 days**

#### Tasks
- [ ] **Task 6.1**: Review all classes for SRP violations
  - Create issue list
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 6.2**: Apply Open/Closed Principle
  - Make layout/theme extensible via plugins
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 6.3**: Apply Liskov Substitution
  - Review inheritance hierarchies
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 6.4**: Apply Interface Segregation
  - Split large interfaces if needed
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 6.5**: Review Dependency Inversion
  - Ensure all dependencies use abstractions
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 7: Code Quality & Patterns ??
**Priority: MEDIUM | Estimated Effort: 3-5 days**

#### Tasks
- [ ] **Task 7.1**: Implement Repository Pattern
  - Abstract data access for projects
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 7.2**: Implement Strategy Pattern
  - Export strategies (single, year, double-sided)
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 7.3**: Implement Factory Pattern
  - ViewModel factories, Renderer factories
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 7.4**: Add FluentValidation
  - Validate CalendarProject, PageSpec, etc.
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 7.5**: Remove magic numbers
  - Create constants class
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 7.6**: Add XML documentation
  - Document all public APIs
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 8: Performance & Resource Management ?
**Priority: MEDIUM | Estimated Effort: 3-5 days**

#### Tasks
- [ ] **Task 8.1**: Implement proper IDisposable pattern
  - For bitmap caches, image processors
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 8.2**: Add memory pressure management
  - Track and limit bitmap memory usage
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 8.3**: Optimize parallel rendering
  - Tune MaxDegreeOfParallelism
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 8.4**: Add progress cancellation tests
  - Ensure clean cancellation
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

### Phase 9: Architecture Documentation ??
**Priority: LOW | Estimated Effort: 2-3 days**

#### Tasks
- [ ] **Task 9.1**: Create architecture diagram
  - Layer diagram (UI, Business, Data)
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 9.2**: Document design patterns used
  - MVVM, Repository, Strategy, etc.
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 9.3**: Create developer onboarding guide
  - How to build, test, extend
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

- [ ] **Task 9.4**: Document testing strategy
  - Unit, integration, UI test approach
  - Status: ? Not Started
  - Assignee: _____
  - Notes: _____

---

## ?? Proposed New Architecture

```
CalendarMaker-MAUI/
??? CalendarMaker.Core/          [NEW - Business Logic]
???? Models/
?   ??? Interfaces/
?   ??? Services/
?   ?   ??? Rendering/
?   ?   ?   ??? ICalendarRenderer.cs
?   ?   ?   ??? CalendarRenderer.cs
?   ?   ?   ??? ILayoutCalculator.cs
?   ?   ?   ??? LayoutCalculator.cs
?   ?   ??? Processing/
?   ?   ? ??? IImageProcessor.cs
?   ?   ?   ??? ImageProcessor.cs
?   ?   ??? Export/
?   ?       ??? IExportStrategy.cs
?   ? ??? SinglePageExportStrategy.cs
?   ?       ??? YearExportStrategy.cs
?   ?       ??? DoubleSidedExportStrategy.cs
?   ??? Validators/
??? CalendarMaker.Infrastructure/        [NEW - Data & I/O]
???? Persistence/
?   ?   ??? IProjectRepository.cs
?   ?   ??? JsonProjectRepository.cs
?   ??? FileSystem/
?   ?   ??? IFileService.cs
??   ??? FileService.cs
?   ??? Caching/
?    ??? IImageCache.cs
?     ??? ImageCache.cs
??? CalendarMaker.Application/           [NEW - Use Cases]
?   ??? Commands/
?   ??? Queries/
?   ??? DTOs/
??? CalendarMaker-MAUI/     [UI Layer]
?   ??? Views/
?   ??? ViewModels/
?   ??? Behaviors/         [NEW]
?   ??? Converters/
?   ??? Services/
?       ??? IDialogService.cs
?       ??? DialogService.cs
?       ??? INavigationService.cs
? ??? NavigationService.cs
??? CalendarMaker.Tests/    [NEW - Tests]
    ??? Unit/
    ?   ??? Services/
    ?   ??? ViewModels/
    ?   ??? Validators/
    ??? Integration/
    ??? Fixtures/
```

---

## ?? Migration Strategy

### Step 1: Create New Projects (Non-Breaking)
- Add CalendarMaker.Core class library
- Add CalendarMaker.Infrastructure class library
- Add CalendarMaker.Tests test project

### Step 2: Extract Services (Incremental)
- Move one service at a time to Core
- Create interface, implementation, tests
- Update DI registration
- Verify functionality

### Step 3: Refactor UI (Carefully)
- Create ViewModel for each Page
- Extract gesture handlers
- One Page at a time

### Step 4: Remove Old Code
- Delete duplicated code
- Clean up unused methods
- Update documentation

---

## ?? Success Metrics

- [ ] DesignerPage.xaml.cs reduced from 1,200+ to < 300 lines
- [ ] RenderAndExport.cs split into 5+ focused classes
- [ ] Zero code duplication (DRY principle)
- [ ] 80%+ unit test coverage
- [ ] All SOLID principles applied
- [ ] Clean Architecture boundaries
- [ ] Zero compiler warnings
- [ ] All services testable in isolation

---

## ?? Testing Strategy

### Unit Tests (Core Layer)
- CalendarEngine month grid generation
- LayoutCalculator slot computation
- ImageProcessor pan/zoom math
- Validators
- Business logic

### Integration Tests (Infrastructure Layer)
- File system operations
- Project serialization/deserialization
- Image import/export

### UI Tests (Optional)
- Page navigation
- User workflows
- Visual regression testing

### Test Coverage Tools
- Coverlet for .NET
- ReportGenerator for reports
- SonarQube for quality gates

---

## ?? Notes

- Keep existing functionality working during refactoring
- Create feature branches for each phase
- Conduct code reviews for all changes
- Update documentation incrementally
- Run full test suite before merging

---

## ?? Getting Started

1. Review this plan with the team
2. Set up test project (Phase 5, Task 5.1)
3. Begin Phase 1 (Foundation)
4. Work through phases sequentially
5. Track progress in this document

---

**Last Updated**: [Current Date]
**Version**: 1.0
**Status**: Planning
