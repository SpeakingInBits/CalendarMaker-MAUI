# CalendarMaker-MAUI Refactoring Tasks

**Quick Reference Task Tracker**

## Legend
- ? Not Started
- ?? In Progress
- ? Complete
- ?? Blocked
- ?? Skipped

---

## Phase 1: Foundation & Infrastructure (HIGH Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 1.1 | Create IDialogService & implementation | ? | | 4h | Abstract DisplayAlert, modals |
| 1.2 | Create INavigationService & implementation | ? | | 4h | Wrap Shell navigation |
| 1.3 | Create IFilePickerService & implementation | ? | | 3h | Abstract FilePicker, FileSaver |
| 1.4 | Implement logging infrastructure | ? | | 2h | Add ILogger support |
| 1.5 | Create centralized exception handling | ? | | 3h | Global error handler |

**Phase 1 Total**: ~16 hours

---

## Phase 2: Extract Rendering Logic (HIGH Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 2.1 | Create ICalendarRenderer interface | ? | | 3h | Define rendering contracts |
| 2.2 | Implement CalendarRenderer | ? | | 12h | Extract all draw logic |
| 2.3 | Create ILayoutCalculator service | ? | | 6h | ComputeSlots, ComputeSplit |
| 2.4 | Create IImageProcessor service | ? | | 8h | Pan/zoom, bitmap caching |
| 2.5 | Create specification objects | ? | | 4h | CalendarGridSpec, PhotoSlotSpec |

**Phase 2 Total**: ~33 hours

---

## Phase 3: Refactor DesignerPage (HIGH Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 3.1 | Create DesignerViewModel | ? | | 12h | Move state from code-behind |
| 3.2 | Extract GestureHandler class | ? | | 8h | Touch/gesture logic |
| 3.3 | Extract CanvasManager class | ? | | 6h | Canvas rendering coord |
| 3.4 | Create PageNavigationManager | ? | | 4h | Page index logic |
| 3.5 | Create PhotoAssignmentCoordinator | ? | | 6h | Photo-to-slot workflow |
| 3.6 | Reduce DesignerPage to <300 lines | ? | | 8h | View init & bindings only |

**Phase 3 Total**: ~44 hours

---

## Phase 4: Improve Service Layer (MEDIUM Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 4.1 | Add validation to ProjectStorageService | ? | | 4h | Validate before save |
| 4.2 | Implement proper error handling | ? | | 6h | Result<T> pattern |
| 4.3 | Refactor AssetService | ? | | 6h | Separate file ops |
| 4.4 | Extract IImageCacheService | ? | | 8h | Centralized caching |
| 4.5 | Refactor PdfExportService | ? | | 12h | Delegate to CalendarRenderer |

**Phase 4 Total**: ~36 hours

---

## Phase 5: Unit Testing Infrastructure (HIGH Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 5.1 | Create test project | ? | | 2h | xUnit/NUnit setup |
| 5.2 | Add Moq/NSubstitute | ? | | 2h | Mocking framework |
| 5.3 | Create test fixtures & builders | ? | | 6h | Test data helpers |
| 5.4 | Write CalendarEngine tests | ? | | 8h | Month grid tests |
| 5.5 | Write LayoutCalculator tests | ? | | 8h | Layout variation tests |
| 5.6 | Write ImageProcessor tests | ? | | 6h | Pan/zoom tests |
| 5.7 | Write AssetService tests | ? | | 8h | Mock file system |
| 5.8 | Write ProjectStorageService tests | ? | | 8h | CRUD operation tests |
| 5.9 | Write ViewModel tests | ? | | 12h | All ViewModels |
| 5.10 | Achieve >80% code coverage | ? | | 16h | Coverage analysis |

**Phase 5 Total**: ~76 hours

---

## Phase 6: Apply SOLID Principles (MEDIUM Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 6.1 | Review all classes for SRP violations | ? | | 6h | Create issue list |
| 6.2 | Apply Open/Closed Principle | ? | | 8h | Plugin extensibility |
| 6.3 | Apply Liskov Substitution | ? | | 4h | Review inheritance |
| 6.4 | Apply Interface Segregation | ? | | 4h | Split large interfaces |
| 6.5 | Review Dependency Inversion | ? | | 4h | Use abstractions |

**Phase 6 Total**: ~26 hours

---

## Phase 7: Code Quality & Patterns (MEDIUM Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 7.1 | Implement Repository Pattern | ? | | 6h | Abstract data access |
| 7.2 | Implement Strategy Pattern | ? | | 8h | Export strategies |
| 7.3 | Implement Factory Pattern | ? | | 6h | ViewModel/Renderer factories |
| 7.4 | Add FluentValidation | ? | | 8h | Model validation |
| 7.5 | Remove magic numbers | ? | | 4h | Constants class |
| 7.6 | Add XML documentation | ? | | 8h | Document public APIs |

**Phase 7 Total**: ~40 hours

---

## Phase 8: Performance & Resource Management (MEDIUM Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 8.1 | Implement IDisposable pattern | ? | | 6h | Bitmap caches |
| 8.2 | Add memory pressure management | ? | | 8h | Limit bitmap memory |
| 8.3 | Optimize parallel rendering | ? | | 6h | Tune parallelism |
| 8.4 | Add progress cancellation tests | ? | | 4h | Clean cancellation |

**Phase 8 Total**: ~24 hours

---

## Phase 9: Architecture Documentation (LOW Priority)

| ID | Task | Status | Assignee | Est. | Notes |
|----|------|--------|----------|------|-------|
| 9.1 | Create architecture diagram | ? | | 4h | Layer diagram |
| 9.2 | Document design patterns | ? | | 4h | MVVM, Repository, etc. |
| 9.3 | Create developer onboarding guide | ? | | 6h | Build, test, extend |
| 9.4 | Document testing strategy | ? | | 3h | Test approach |

**Phase 9 Total**: ~17 hours

---

## Summary

| Phase | Priority | Est. Hours | Status |
|-------|----------|------------|--------|
| Phase 1: Foundation | HIGH | 16h | ? |
| Phase 2: Rendering | HIGH | 33h | ? |
| Phase 3: DesignerPage | HIGH | 44h | ? |
| Phase 4: Services | MEDIUM | 36h | ? |
| Phase 5: Testing | HIGH | 76h | ? |
| Phase 6: SOLID | MEDIUM | 26h | ? |
| Phase 7: Quality | MEDIUM | 40h | ? |
| Phase 8: Performance | MEDIUM | 24h | ? |
| Phase 9: Documentation | LOW | 17h | ? |
| **TOTAL** | | **~312 hours** | |

---

## Current Sprint (Example - Update as needed)

**Sprint Goal**: Set up testing infrastructure and create dialog service

**Sprint Tasks**:
- [ ] 5.1: Create test project
- [ ] 5.2: Add mocking framework
- [ ] 1.1: Create IDialogService

**Blockers**: None

**Notes**: 
- Start with test project to enable TDD approach
- DialogService needed for DesignerPage refactoring

---

## Completed Tasks Log

| Date | Task ID | Task | Duration | Notes |
|------|---------|------|----------|-------|
| ___ | ___ | ___ | ___ | ___ |

---

## Issues & Risks

| ID | Issue | Severity | Mitigation | Status |
|----|-------|----------|------------|--------|
| R1 | Large refactoring may introduce bugs | HIGH | Comprehensive testing, incremental approach | ? |
| R2 | Time estimate may be off | MEDIUM | Re-estimate after Phase 1 | ? |
| R3 | Team availability | MEDIUM | Prioritize HIGH priority tasks | ? |

---

## Quick Commands

### Create Test Project
```bash
dotnet new xunit -n CalendarMaker.Tests
dotnet add CalendarMaker.Tests reference CalendarMaker-MAUI/CalendarMaker-MAUI.csproj
dotnet add CalendarMaker.Tests package Moq
dotnet add CalendarMaker.Tests package FluentAssertions
dotnet add CalendarMaker.Tests package coverlet.collector
```

### Run Tests
```bash
dotnet test CalendarMaker.Tests
dotnet test CalendarMaker.Tests --collect:"XPlat Code Coverage"
```

### Check Coverage
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport"
```

---

**Last Updated**: [Current Date]
**Current Phase**: Planning
**Overall Progress**: 0/62 tasks (0%)
