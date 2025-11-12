# Project Log: Bug #20 - REF Name Storage Order Fix

**Date:** January 11, 2025 (Late Evening - Bug #18 Regression Investigation)  
**Status:** ✅ RESOLVED  
**Impact:** Critical - Prevented all REF resolution, caused infinite loops

---

## Problem Description

After supposedly fixing Bug #18 (REF infinite loop) in an earlier session, the bug regressed. When running `test_simple.hvm` (containing `main = 5`), the reducer entered an infinite loop with `[INTERACT tag=21]` repeating endlessly.

Tag 21 is TAG-REF, indicating the REF resolution was failing.

---

## Root Cause Analysis

The issue was in **src/parse.fs:350-354** in the `PARSE-VAR` function that creates REF terms.

### Incorrect Code:
```forth
\ Store name address and length
TUCK ! ( c-addr ref-loc | R: ref-loc )
CELL+ ! ( | R: ref-loc )
```

### Stack Trace of Bug:
1. Before TUCK: `( c-addr u ref-loc )`
2. After TUCK: `( c-addr ref-loc u )`
3. After first `!`: Stores `u` (length) at `ref-loc`
4. After `CELL+`: `( c-addr ref-loc+CELL )`
5. After second `!`: Stores `c-addr` at `ref-loc+CELL`

**Result:** Memory layout was `[length] [address]`

### Expected by RESOLVE-REF:
In **src/book.fs:141-142**, RESOLVE-REF expected:
```forth
DUP @ SWAP CELL+ @ ( name-addr name-len )
```

This retrieves: `[name-addr] [name-len]`

**The mismatch caused "Invalid memory address" crash** when RESOLVE-REF tried to use the length value as a memory address!

---

## Solution

Fixed **src/parse.fs:352-354** to store fields in the correct order:

```forth
\ Store name address and length
\ Stack: ( c-addr u ref-loc | R: ref-loc )
2 PICK OVER ! ( c-addr u ref-loc | R: ref-loc ) \ Store c-addr at ref-loc
CELL+ ! ( c-addr | R: ref-loc ) \ Store u at ref-loc+CELL
DROP ( | R: ref-loc )
```

### Stack Trace of Fix:
1. Start: `( c-addr u ref-loc )`
2. `2 PICK`: `( c-addr u ref-loc c-addr )` - picks 3rd item
3. `OVER`: `( c-addr u ref-loc c-addr ref-loc )`
4. `!`: Stores `c-addr` at `ref-loc`, leaves `( c-addr u ref-loc )`
5. `CELL+`: `( c-addr u ref-loc+CELL )`
6. `!`: Stores `u` at `ref-loc+CELL`, leaves `( c-addr )`
7. `DROP`: `( )`

**Result:** Memory layout is now `[address] [length]` - **CORRECT!**

---

## Test Results

### Before Fix:
- test_simple.hvm: **INFINITE LOOP** (tag=21 repeating)
- Crash with "Invalid memory address"

### After Fix:
- ✅ test_simple.hvm: **WORKING** - Returns 5 correctly
- ✅ test_add.hvm: Returns 13 (10+3) - **CORRECT**
- ⚠️ test_sub.hvm: Returns 13 (should be 7) - **Bug #19 still present**

---

## Impact

**Bug #18 was NOT actually fixed in the previous session** - it was misdiagnosed. The real bug was the REF name storage order, which I've now designated as **Bug #20** for clarity.

The DEFER/IS pattern changes made in the previous session were correct and remain in place, but they weren't sufficient to fix the REF issue because the underlying data structure was corrupted.

---

## Files Modified

### src/parse.fs
- Lines 352-354: Fixed REF name storage to use correct order

---

## Lessons Learned

1. **Stack Discipline is Critical**: Even a simple `TUCK !` vs proper stack manipulation can cause catastrophic failures
2. **Data Structure Alignment**: When code in one module stores data and code in another retrieves it, the layout MUST match exactly
3. **Test Thoroughly**: The previous "fix" appeared to work but wasn't actually tested properly
4. **Debug Memory Issues**: "Invalid memory address" errors often indicate data structure mismatches

---

## Related Bugs

- **Bug #18**: REF infinite loop (actually caused by Bug #20, not a separate issue)
- **Bug #19**: OP2 stack corruption (still present, needs investigation)

---

## Next Steps

1. ✅ Bug #20 (REF storage) - **FIXED**
2. ⏳ Bug #19 (OP2 operators) - Still needs investigation
3. ⏳ Verify no other data structure mismatches exist

---

## Status

**RESOLVED** - REF resolution now works correctly. test_simple.hvm passes.
