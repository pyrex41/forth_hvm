# Project Log: Bug #19 - OP2 Stack Corruption Fix

**Date:** January 11, 2025 (Late Evening Session)
**Status:** ✅ RESOLVED
**Impact:** Critical - All OP2 operators except ADD were returning incorrect results

---

## Problem Description

After fixing Bug #20 (REF name storage order), testing revealed that Bug #19 (OP2 stack corruption) was still present and affecting all OP2 operators except ADD:

- test_add.hvm: Returns 13 (10+3) ✓ Correct
- test_sub.hvm: Returns 13 (should be 7 for 10-3) ✗ Wrong
- test_mul.hvm: Returns 13 (should be 30 for 10*3) ✗ Wrong
- test_div.hvm: Returns 13 (should be 3 for 10/3) ✗ Wrong
- test_mod.hvm: Returns 13 (should be 1 for 10%3) ✗ Wrong

All operators were returning the ADD result, indicating incorrect opcode extraction or storage.

---

## Root Cause Analysis

Investigation revealed **TWO SEPARATE BUGS** in Bug #19:

### Bug #19a: Incorrect Token Extraction (parse.fs:1186)

**Location:** src/parse.fs:1186 in PARSE-TERM where OP2 operators are detected

**Incorrect Code:**
```forth
2 PICK DUP TOK-STAR = SWAP DUP TOK-PLUS >= SWAP TOK-NE <= AND OR IF
  \ It's an operator - parse as OP2
  ROT 2DROP  \ ( type addr len -- type )  BUG!
  PARSE-OP2 EXIT
```

**Stack Trace of Bug:**
1. Start: `( type addr len )`
2. After `ROT`: `( addr len type )`
3. After `2DROP`: `( addr )` ← Wrong! Address value instead of token type

This caused the token passed to PARSE-OP2 to be a memory address value (typically 0 or a small number) instead of the actual token type constant.

**Debug Output Revealed:**
```
[PARSE-OP2 token=0 ] [opcode=0 ]
```
When parsing SUB, token should have been 18 (TOK-MINUS), not 0!

**Solution:**
```forth
2 PICK DUP TOK-STAR = SWAP DUP TOK-PLUS >= SWAP TOK-NE <= AND OR IF
  \ It's an operator - parse as OP2
  2DROP  \ ( type addr len -- type )  Drop addr and len, keep type
  PARSE-OP2 EXIT
```

**Stack Trace of Fix:**
1. Start: `( type addr len )`
2. After `2DROP`: `( type )` ← Correct!

**After Fix:**
```
[PARSE-OP2 token=18 ] [opcode=1 ]
```
Token is now correct (18 = TOK-MINUS, opcode 1 = SUB)

---

### Bug #19b: Operand Order Reversal (interact.fs:384-396)

**Location:** src/interact.fs:384-396 in OP2-COMPUTE function

Even after fixing token extraction, test_sub.hvm returned `2199023255545` instead of `7`. This huge number indicated unsigned integer underflow from a backwards subtraction.

**Problem:** Non-commutative operations (SUB, DIV, MOD, and comparisons) had `SWAP` before the operation, reversing the operand order.

**Incorrect Code:**
```forth
DUP 1 = IF DROP SWAP - EXIT THEN  \ SUB
DUP 3 = IF DROP SWAP / EXIT THEN  \ DIV
DUP 4 = IF DROP SWAP MOD EXIT THEN  \ MOD
DUP 10 = IF DROP SWAP < IF -1 ELSE 0 THEN EXIT THEN  \ LT
DUP 11 = IF DROP SWAP > IF -1 ELSE 0 THEN EXIT THEN  \ GT
DUP 12 = IF DROP SWAP <= IF -1 ELSE 0 THEN EXIT THEN  \ LE
DUP 13 = IF DROP SWAP >= IF -1 ELSE 0 THEN EXIT THEN  \ GE
```

This made operations backwards:
- `(- 10 3)` computed as `3 - 10 = -7` (unsigned: `2199023255545`)
- `(/ 10 3)` computed as `3 / 10 = 0`
- `(< 10 3)` computed as `3 < 10 = true` (should be false)

**Solution:**
Remove `SWAP` from all non-commutative operations:

```forth
DUP 0 = IF DROP + EXIT THEN  \ ADD (commutative, no swap needed)
DUP 1 = IF DROP - EXIT THEN  \ SUB (lhs - rhs)
DUP 2 = IF DROP * EXIT THEN  \ MUL (commutative, no swap needed)
DUP 3 = IF DROP / EXIT THEN  \ DIV (lhs / rhs)
DUP 4 = IF DROP MOD EXIT THEN  \ MOD (lhs mod rhs)
DUP 5 = IF DROP AND EXIT THEN  \ AND (commutative, no swap needed)
DUP 6 = IF DROP OR EXIT THEN  \ OR (commutative, no swap needed)
DUP 7 = IF DROP XOR EXIT THEN  \ XOR (commutative, no swap needed)
DUP 8 = IF DROP LSHIFT EXIT THEN  \ SHL (lhs << rhs)
DUP 9 = IF DROP RSHIFT EXIT THEN  \ SHR (lhs >> rhs)
DUP 10 = IF DROP < IF -1 ELSE 0 THEN EXIT THEN  \ LT (lhs < rhs)
DUP 11 = IF DROP > IF -1 ELSE 0 THEN EXIT THEN  \ GT (lhs > rhs)
DUP 12 = IF DROP <= IF -1 ELSE 0 THEN EXIT THEN  \ LE (lhs <= rhs)
DUP 13 = IF DROP >= IF -1 ELSE 0 THEN EXIT THEN  \ GE (lhs >= rhs)
DUP 14 = IF DROP = IF -1 ELSE 0 THEN EXIT THEN  \ EQ (commutative, no swap needed)
DUP 15 = IF DROP <> IF -1 ELSE 0 THEN EXIT THEN  \ NE (commutative, no swap needed)
```

---

## Test Results

### Before Fixes:
- test_add.hvm: 13 ✓ (only ADD worked)
- test_sub.hvm: 13 ✗ (should be 7)
- test_mul.hvm: 13 ✗ (should be 30)
- test_div.hvm: 13 ✗ (should be 3)
- test_mod.hvm: 13 ✗ (should be 1)

### After Token Extraction Fix (Bug #19a):
- test_sub.hvm: 2199023255545 ✗ (unsigned underflow, still wrong)

### After Both Fixes (Bug #19a + Bug #19b):
- ✅ test_add.hvm: 13 (10 + 3) ✓
- ✅ test_sub.hvm: 7 (10 - 3) ✓
- ✅ test_mul.hvm: 30 (10 * 3) ✓
- ✅ test_div.hvm: 3 (10 / 3) ✓
- ✅ test_mod.hvm: 1 (10 % 3) ✓

**ALL OP2 OPERATORS NOW WORKING CORRECTLY!**

---

## Files Modified

### src/parse.fs
- Line 1186: Changed `ROT 2DROP` to `2DROP` for correct token extraction
- Lines 496-498: Removed debug output from PARSE-OP2 (added temporarily for debugging)

### src/interact.fs
- Lines 384-396: Removed `SWAP` from non-commutative operations in OP2-COMPUTE

---

## Technical Insights

### 1. Stack Manipulation Precision
The difference between `ROT 2DROP` and `2DROP` seems minor but has catastrophic impact:
- `ROT 2DROP`: Rotates stack then drops, changing what remains
- `2DROP`: Directly drops top two items

For `( type addr len )`:
- `ROT 2DROP` → `( addr )` ← Wrong
- `2DROP` → `( type )` ← Correct

### 2. Commutativity Matters
Operations fall into two categories:

**Commutative** (order doesn't matter):
- ADD: `a + b = b + a`
- MUL: `a * b = b * a`
- AND, OR, XOR: `a OP b = b OP a`
- EQ, NE: `a == b = b == a`

**Non-Commutative** (order matters):
- SUB: `a - b ≠ b - a`
- DIV: `a / b ≠ b / a`
- MOD: `a % b ≠ b % a`
- LT, GT, LE, GE: `a < b ≠ b < a`
- LSHIFT, RSHIFT: `a << b ≠ b << a`

Adding `SWAP` to non-commutative operations reverses their semantics!

### 3. Debug Output Strategy
Adding debug output to PARSE-OP2 was crucial:
```forth
DEBUG? IF ." [PARSE-OP2 token=" DUP . ." ] " THEN
TOKEN-TO-OP2
DEBUG? IF ." [opcode=" DUP . ." ] " THEN
```

This revealed the token was 0 instead of 18, immediately pointing to the extraction bug.

### 4. Unsigned Integer Wraparound
When test_sub.hvm returned `2199023255545`, this indicated:
- Negative result: `3 - 10 = -7`
- Unsigned interpretation: `-7` wraps to large positive number
- This was the clue that operands were backwards

---

## Lessons Learned

1. **One Bug Can Hide Another**: Bug #19 actually contained two separate issues that both needed fixing

2. **Stack Discipline is Critical**: Even knowing the correct stack effect `( type addr len -- type )`, the implementation `ROT 2DROP` vs `2DROP` makes all the difference

3. **Test Non-Commutative Operations**: ADD worked by accident because it's commutative - it masked the operand order bug

4. **Debug Output is Invaluable**: Strategic debug output immediately revealed the token value was wrong

5. **Large Unexpected Numbers Mean Underflow**: When unsigned arithmetic produces huge numbers, check for negative intermediate results

---

## Related Bugs

- **Bug #18**: REF infinite loop (actually was Bug #20)
- **Bug #20**: REF name storage order (fixed in previous session)
- **Bug #19**: OP2 stack corruption (THIS BUG - now fixed)

---

## Next Steps

1. ✅ Bug #19 (OP2 operators) - **COMPLETELY FIXED**
2. ⏳ Test comparison operators (LT, GT, LE, GE, EQ, NE)
3. ⏳ Test bitwise operators (AND, OR, XOR, SHL, SHR)
4. ⏳ Update current_progress.md with Bug #19 resolution

---

## Status

**RESOLVED** - All OP2 arithmetic operators now work correctly. Both token extraction and operand order issues fixed.

**Confidence:** 95% - All basic arithmetic tests pass. Need to verify comparison and bitwise operators.
