# Project Log: Bug #16 Fix - OP2 Detection and Parsing

**Date:** January 11, 2025 (Evening Session Continuation)
**Duration:** ~2 hours
**Status:** ✅ Partial Success - Major Bug Fixed, One Remaining Issue

---

## Session Summary

Fixed critical Bug #16 in OP2 operator detection that was preventing arithmetic operations from parsing correctly. Made significant progress on Bug #17 (OP2 operand storage), though complete resolution is pending.

### Key Achievement
✅ **test_simple.hvm continues to work perfectly** - No regressions introduced

---

## Bugs Addressed

### Bug #16: OP2 Operator Detection Stack Error ✅ FIXED
**Location:** `src/parse.fs:1176`

**Root Cause:**
The check for OP2 operators was using incorrect stack manipulation:
```forth
DUP TOK-PLUS >= OVER TOK-NE <= AND
```

With stack `( type addr len )`:
- `DUP` → `( type addr len type )`
- `TOK-PLUS >=` → `( type addr len flag1 )`
- `OVER` → `( type addr len flag1 len )` ❌ WRONG! Should compare type, not len
- `TOK-NE <=` → Compares len with TOK-NE, always fails

**Fix:**
```forth
2 PICK DUP TOK-PLUS >= SWAP TOK-NE <= AND
```

With stack `( type addr len )`:
- `2 PICK` → `( type addr len type )`
- `DUP TOK-PLUS >=` → `( type addr len type flag1 )`
- `SWAP` → `( type addr len flag1 type )`
- `TOK-NE <=` → `( type addr len flag1 flag2 )`
- `AND` → Correctly checks if type is an operator

**Impact:**
- OP2 expressions like `(+ 2 3)` now correctly parse as OP2 instead of APP
- Parser now recognizes all 16 binary operators (`, -, *, /, %, &, |, ^, <<, >>, <, >, <=, >=, ==, !=)

### Bug #17: OP2 Operand Storage Order ⚠️ IN PROGRESS
**Location:** `src/parse.fs:514-520`

**Problem:**
After parsing, OP2 terms store operands in memory but LHS gets corrupted when retrieved.

**Investigation Findings:**
- ✅ PARSE-TERM correctly returns terms with proper values (LHS=2, RHS=3)
- ✅ Storage operations work in isolation (test_storage.fs verified)
- ✅ PARSE-APP pattern copied correctly
- ❌ LHS becomes corrupted (tag=19, val=0) while RHS remains correct (tag=7, val=3)
- ❌ Result reduces to λx0 instead of U32 value 5

**Attempted Fixes:**
1. Tried multiple stack manipulation patterns for storage
2. Copied exact pattern from working PARSE-APP code
3. Added R-stack juggling to preserve values
4. Current implementation matches PARSE-APP but issue persists

**Current Code:**
```forth
2 ALLOC ( lhs rhs op2-loc | R: opcode )
DUP >R ( lhs rhs op2-loc | R: opcode op2-loc )
2 PICK OVER ! ( lhs rhs op2-loc | R: opcode op2-loc ) \ Store lhs at op2-loc
CELL+ ! ( lhs | R: opcode op2-loc ) \ Store rhs at op2-loc+CELL
DROP ( | R: opcode op2-loc )

TAG-OP2 R> R> SWAP PACK-TERM
```

**Next Steps for Bug #17:**
- Investigate if ALLOC is returning valid memory addresses
- Check if there's a memory alignment issue specific to OP2
- Verify heap management isn't corrupting stored values
- Consider if term encoding/decoding has issues with OP2 layout

---

## Test Results

### Passing Tests
- ✅ **test_simple.hvm** (`main = 5`): **PASSING**
  - Loads correctly
  - Reduces to U32 value 5
  - No regressions from changes

### Failing Tests
- ⚠️ **examples/test_add.hvm** (`main = (+ 2 3)`): **FAILING**
  - Now parses as OP2 (Bug #16 fixed) ✓
  - OP2-U32 called correctly ✓
  - Operand storage corrupted (Bug #17) ✗
  - Returns `λx0 .` instead of `5`
  - Crashes in .TERM with "Invalid memory address"

---

## Code Changes

### Modified Files
1. **src/parse.fs**
   - Line 1176: Fixed OP2 operator detection (Bug #16)
   - Lines 514-520: Attempted fix for OP2 operand storage (Bug #17 - incomplete)

### Debug Files Created (Not Committed)
- `debug_book.fs` - Book loading inspection
- `debug_main.fs` - Main term analysis
- `debug_op2.fs` - OP2 parsing debug
- `debug_op2_2.fs` - OP2 memory inspection
- `debug_op2_reduce.fs` - OP2-U32 execution trace
- `debug_parse_op2.fs` - PARSE-OP2 step-by-step
- `debug_parse_terms.fs` - PARSE-TERM verification
- `debug_reduce.fs` - Reduction tracing
- `test_debug.fs` - DEBUG mode testing
- `test_pack.fs` - PACK-TERM validation
- `test_run.fs` - RUN-FILE testing
- `test_storage.fs` - Memory storage verification

---

## Technical Details

### OP2 Term Structure
```
Layout: tag:5b lab:18b val:41b
- tag: TAG-OP2 = 8
- lab: opcode (0=ADD, 1=SUB, 2=MUL, ...)
- val: pointer to [lhs_term, rhs_term]
```

### Expected Memory Layout
```
op2-loc[0] = lhs_term (e.g., U32 with val=2)
op2-loc[1] = rhs_term (e.g., U32 with val=3)
```

### OP2-U32 Retrieval Pattern
```forth
DUP @ ( op2-loc lhs-term )
SWAP CELL+ @ ( lhs-term rhs-term )
```

---

## Task-Master Updates

**Task 8: Implement Core Interaction Rules** (In Progress)
- Subtask 8.10: OP2-U32 implementation
  - Fixed Bug #16: OP2 operator detection ✓
  - Working on Bug #17: OP2 operand storage ⚠️
  - OP2-U32 interaction rule functional when given correct input
  - Parser integration needs completion

---

## Performance & Metrics

### Compilation
- ✅ Clean compilation with no errors
- ✅ All 31 unit tests still pass
- ✅ No regressions in existing functionality

### Code Quality
- Clear separation between Bug #16 (fixed) and Bug #17 (in progress)
- Extensive debugging infrastructure created
- Isolated test cases confirm each component works individually

---

## Lessons Learned

### Stack Discipline
1. **2 PICK vs OVER:** When you need an item deep in the stack, use 2 PICK, not OVER
2. **Token Triple:** Parser tokens are `( type addr len )` - always use 2 PICK for type
3. **Verification:** Create isolated test cases for each stack manipulation pattern

### Debugging Strategy
1. Test each component in isolation before integration
2. Compare working patterns (PARSE-APP) to broken ones (PARSE-OP2)
3. Add debug output at each step to track data corruption points
4. Verify assumptions with simple test cases

### Memory Management
1. ALLOC appears to work correctly in isolation
2. Storage operations (`!`, `@`) work correctly with direct testing
3. Issue manifests only when integrated into full parser flow
4. Suggests timing or state-related corruption rather than fundamental bug

---

## Next Session Goals

1. **Complete Bug #17 Fix**
   - Investigate ALLOC behavior in parser context
   - Check for stack corruption between PARSE-TERM calls
   - Verify R-stack state management
   - Consider alternative storage approach if pattern matching fails

2. **Get test_add.hvm Working**
   - Achieve correct result: `5`
   - Verify all 16 OP2 operators work
   - Test with more complex expressions

3. **Fix .TERM Crash**
   - Handle non-U32 results gracefully
   - Add proper λ-term printing support

4. **Expand Testing**
   - Test arithmetic operations from examples/
   - Validate all OP2 opcodes
   - Check edge cases (overflow, division by zero, etc.)

---

## Confidence Assessment

**Bug #16:** 100% - Fixed and verified
**Bug #17:** 60% - Root cause unclear despite extensive investigation
**Overall Session:** 75% - Made solid progress but incomplete

### Reasoning
- ✅ Successfully identified and fixed critical parser bug
- ✅ No regressions in existing functionality
- ⚠️ Bug #17 proving more complex than expected
- ⚠️ Storage pattern copied correctly but issue persists
- ⚠️ Suggests deeper issue with heap, state, or timing

---

## Files Modified

```
src/parse.fs (2 changes)
- Line 1176: OP2 detection fix
- Lines 514-520: OP2 storage pattern (incomplete fix)
```

---

## Conclusion

Significant progress on OP2 support with Bug #16 completely resolved. Bug #17 requires deeper investigation into heap management or parser state. The runtime core remains solid with test_simple.hvm continuing to work perfectly.

**Status:** 🟡 Partial Success - One bug fixed, one in progress
**Next Priority:** Complete Bug #17 to enable arithmetic operations
