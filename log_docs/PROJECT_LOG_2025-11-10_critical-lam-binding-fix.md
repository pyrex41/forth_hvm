# Project Log: Critical LAM Binding Fix
**Date:** November 10, 2025 (Late Night - Session 4)
**Session Duration:** ~2 hours
**Focus:** Debugging and fixing critical LAM body storage issue

## Session Summary
Fixed a fundamental design flaw in variable binding that was preventing beta reduction from working. The issue involved using heap addresses (which require 33+ bits) in the 18-bit label field of LAM terms. Implemented a binding ID counter system and fixed multiple stack manipulation bugs in the substitution logic.

## Changes Made

### 1. Parser Module (parse.fs)
**Added Binding ID Counter System:**
- Added `BIND-ID` variable and `FRESH-BIND-ID` word (parse.fs:13-19)
- Binding IDs are small integers (1, 2, 3...) that fit in 18-bit label field
- Replaces previous approach of using heap addresses as binding identifiers

**Fixed PARSE-LAM:**
- Changed from allocating heap space for bindings to using fresh bind-id (parse.fs:305-310)
- Fixed PACK-TERM stack order: added `SWAP` before `TAG-LAM -ROT` (parse.fs:322)
  - Was packing: `(tag=0, lab=lam-loc, val=bind-id)` - WRONG
  - Now packs: `(tag=0, lab=bind-id, val=lam-loc)` - CORRECT
- LAM terms now correctly store: label=binding-id, value=heap-location-of-body

### 2. Interaction Module (interact.fs)
**Fixed SUBST-WALK VAR Case:**
- Fixed return stack manipulation when VAR matches binding (interact.fs:18)
- Was returning: `var-loc` (wrong)
- Now returns: `arg-term` (correct)
- Changed from `DROP R> R> DROP` to `DROP R> DROP R>`

**Completely Rewrote APP-LAM Stack Manipulation:**
- Simplified complex stack juggling (interact.fs:63-81)
- Fixed extraction of fun-term, arg-term, var-loc, and body-term
- Corrected sequence:
  1. Get app-loc from app-term
  2. Fetch fun-term and arg-term from heap
  3. Extract var-loc (LAM's label) and body (from LAM's val pointer)
  4. Build correct stack for SUBST-WALK: `(term var-loc arg-term)`
  5. NIP to remove app-term after substitution

**Removed Debug Output:**
- Cleaned up most debug printing from APP-LAM (some remains in parse.fs for now)

## Root Cause Analysis

### The Core Problem
The IC term packing format allocates only 18 bits for the `lab` field:
```
Layout: tag:5b lab:18b val:41b (total 64 bits)
lab field max: 2^18-1 = 262,143
```

But heap addresses on 64-bit systems require 33+ bits. The parser was trying to store heap addresses as LAM labels, causing:
1. **Truncation:** Only lower 18 bits of heap address stored
2. **Corruption:** GET-LAB returned wrong values
3. **Crashes:** Invalid memory access during substitution

### The Solution
Introduced a separate binding ID counter that produces small sequential integers (1, 2, 3...) that fit comfortably in 18 bits. Variable bindings now work like this:

1. **During Parsing:**
   - `λx.body` → allocate bind-id=1
   - Store `x → 1` in substitution map
   - When parsing `x` → create VAR term with val=1
   - Store body in heap, create LAM term with lab=1, val=heap-loc

2. **During Reduction:**
   - Extract bind-id from LAM's label
   - Walk body, find VAR terms where val=bind-id
   - Replace matching VARs with argument

## Bug Trail

1. **Initial symptom:** Test 6 times out, `[APP-LAM body=0]`
2. **First discovery:** LAM body being stored but fetching returns 0
3. **Heap debug:** Body stored at loc=X, but GET-VAL returns loc=Y
4. **Breakthrough:** Realized lab and val were swapped in PACK-TERM
5. **Root cause:** Heap addresses don't fit in 18-bit label field
6. **Final bugs:** Stack manipulation errors in SUBST-WALK and APP-LAM

## Test Results

**Before:** 30/31 tests passing (96.8%)
- Test 6 (beta reduction) timing out

**After:** 31/31 tests passing (100%)
```
Test 6: Parse and reduce (.x x *)...
[SUBST tag=4] [done] [reduced tag=5] PASS (reduced to ERA)
```

All modules passing:
- ✅ Core: 3/3
- ✅ Heap: 6/6
- ✅ Substitution: 4/4
- ✅ Parser: 11/11
- ✅ Reducer: 6/6

## Code Statistics

**Lines Modified:**
- parse.fs: +23 lines (binding ID system, fixed PARSE-LAM)
- interact.fs: -11 lines (simplified APP-LAM, fixed SUBST-WALK)
- Net: +12 LOC

**Key Functions Modified:**
- `PARSE-LAM` (parse.fs:295-327)
- `SUBST-WALK` (interact.fs:8-61)
- `APP-LAM` (interact.fs:63-81)

## Task-Master Status

**Task 7: Implement Reducer and WHNF Loop**
- Status: In Progress → Should move to Done
- All subtasks complete, WHNF loop fully working

**Task 8: Implement Core Interaction Rules**
- Status: Pending → Should move to In Progress
- APP-LAM: Complete and working ✅
- APP-ERA: Already complete ✅
- APP-SUP: Scaffolding complete
- DUP-ERA: Stub only
- DUP-SUP: Partial (label matching)
- DUP-LAM: Stub only

## Current State

**Working:**
- Parser: Full IC grammar with label parsing
- WHNF reduction loop with value checking
- Beta reduction: `(λx.body arg) → body[x:=arg]`
- Variable substitution with SUBST-WALK
- Erasure application: `(* a) → *`

**Partial:**
- Superposition application (scaffolding)
- Superposition duplication (label matching only)

**Not Implemented:**
- Lambda duplication
- Full superposition rules
- SUP/DUP in SUBST-WALK

## Next Steps

1. **Update task-master:** Mark Task 7 done, Task 8 in-progress with implementation notes
2. **Complete remaining rules:** DUP-LAM, full DUP-SUP, APP-SUP
3. **Add SUP/DUP to SUBST-WALK:** Recursive substitution for these constructs
4. **Remove debug output:** Clean up all temporary debug printing
5. **Integration tests:** Test with complex IC programs
6. **Move to Task 9:** Extended interaction rules (numbers, operations)

## Lessons Learned

1. **Bit field constraints matter:** Always verify values fit in allocated bit fields
2. **Stack discipline is critical:** Forth stack manipulation requires extreme care
3. **Test-driven debugging:** The failing test guided us directly to the bug
4. **Incremental fixes:** Each fix revealed the next issue in the chain
5. **Comments lie:** Stack comments were wrong, had to trace manually

## Files Modified

- `src/parse.fs` - Added binding ID system, fixed LAM packing
- `src/interact.fs` - Fixed substitution return, rewrote APP-LAM

## Commit Message
```
fix: Resolve critical LAM binding and beta reduction bugs

- Add BIND-ID counter for 18-bit-compatible variable bindings
- Fix PARSE-LAM to pack bind-id in label, heap-loc in value
- Fix SUBST-WALK to return arg-term instead of var-loc
- Rewrite APP-LAM stack manipulation for correct substitution
- All 31 tests now passing (was 30/31)

Root cause: Heap addresses (33+ bits) don't fit in 18-bit label field.
Solution: Use small sequential binding IDs instead.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
```
