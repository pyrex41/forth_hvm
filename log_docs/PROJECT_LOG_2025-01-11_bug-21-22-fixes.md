# Project Log: 2025-01-11 - Bug #21 & #22 Critical Fixes

## Session Summary
Fixed two critical bugs affecting REF term memory management and stuck term handling in the reduction engine. Both bugs caused crashes with "Invalid memory address" errors. System now gracefully handles invalid terms and properly manages memory for function references.

## Changes Made

### Bug #21: REF Memory Management (src/parse.fs:346-368)
**Issue**: PARSE-VAR stored direct pointers to INPUT-BUF (temporary buffer) when creating REF terms. By the time LINK-REFS ran to resolve references, INPUT-BUF had been reused for other parsing, causing dangling pointer crashes.

**Root Cause**: Classic dangling pointer issue - storing pointers to temporary memory that gets reused.

**Fix**: Modified PARSE-VAR to allocate heap space for REF name strings and copy them using CMOVE pattern from BOOK-PUT:
1. Allocate heap for actual name string characters: `DUP ALLOC`
2. Copy string to heap using CMOVE: `2 PICK OVER 3 PICK CMOVE`
3. Allocate separate ref structure (2 cells) for [name-addr][name-len]
4. Store heap-allocated name address and length in ref structure

**Test**: test_nested_ref.hvm (`f = 5`, `main = @f`) now returns correct result: 5

### Bug #22: Stuck Term Handling (src/reduce.fs:115-126)
**Issue**: When WHNF encountered a stuck term (e.g., APP(U32, arg) where U32 cannot be applied as a function), INTERACT-STEP returned 0, but WHNF didn't preserve the original term. This caused subsequent code to interpret 0 as a term value and crash.

**Root Cause**: WHNF's reduction loop consumed the term when calling INTERACT-STEP. When INTERACT-STEP returned 0 (stuck term indicator), the original term was lost from the stack.

**Fix**: Modified WHNF to save original term before each INTERACT-STEP call:
```forth
: WHNF ( term -- whnf-term )
  BEGIN
    DUP IS-VALUE? 0= WHILE
    DUP >R  \ Save original term on return stack
    INTERACT-STEP
    DUP 0= IF
      \ Stuck term: drop 0, restore original term from R-stack, exit
      DROP R> EXIT
    THEN
    R> DROP  \ Drop saved term since we got a valid result
  REPEAT
;
```

**Test**: test_app_with_ref.hvm (`f = 5`, `main = (@f 3)`) now returns stuck term `(<unknown> 3)` instead of crashing.

### Regression Testing
All existing tests pass:
- test_simple.hvm: 5 ✓
- test_add.hvm: 13 ✓
- test_mul.hvm: 30 ✓
- test_sub.hvm: 7 ✓
- test_div.hvm: 3 ✓
- All OP2 operators verified working

## Test Files Created
- `test_nested_ref.hvm`: Tests Bug #21 (nested @ref)
- `test_app.hvm`: Tests APP with ERA functions
- `test_app_with_ref.hvm`: Tests Bug #22 (invalid APP)
- `test_era.hvm`: Tests ERA node handling
- `test_bugs.fs`: Test harness for bug reproduction

## Technical Details

### PARSE-VAR Fix (src/parse.fs:346-368)
Key pattern used from BOOK-PUT for string copying:
- `2 PICK OVER 3 PICK CMOVE` - Copies string while preserving stack
- `ROT DROP SWAP` - Rearranges stack to match expected layout
- Separate allocation for string data vs ref structure

### WHNF Fix (src/reduce.fs:115-126)
Stack flow for stuck term handling:
1. Entry: `( term )`
2. `DUP >R`: `( term term | R: term )`
3. `INTERACT-STEP`: `( term 0 | R: term )` (if stuck)
4. `DROP R>`: `( term )` - Restore original
5. `EXIT`: Return stuck term unchanged

## Current Status
- **Bug #21**: ✅ FIXED - REF memory properly allocated on heap
- **Bug #22**: ✅ FIXED - Stuck terms handled gracefully
- **Multi-function support**: ✅ Working
- **OP2 operators**: ✅ All 16 operators verified
- **Regression tests**: ✅ All passing

## Next Steps (Full HVM3 Parity)
1. **Phase 1**: Complete DUP interactions (DUP-LAM, DUP-SUP, DUP-U32, DUP-OP2, DUP-MATCH)
2. **Phase 2**: Complete APP interactions (APP-CTR annihilation)
3. **Phase 3**: Implement MATCH term parsing and reduction
4. **Phase 4**: Implement CTR (Constructor) term parsing
5. **Phase 5**: Add file I/O for loading .hvm files
6. **Phase 6**: Comprehensive test suite for all features

## Code References
- src/parse.fs:346-368 - PARSE-VAR REF memory fix
- src/reduce.fs:115-126 - WHNF stuck term handling
- src/book.fs:23-57 - BOOK-PUT pattern (reference for fix)
- src/reduce.fs:31-60 - INTERACT-STEP stuck term logic
