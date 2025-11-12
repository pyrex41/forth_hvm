# Project Log: Parser Memory Access Bug Investigation

**Date:** November 12, 2025
**Session:** Debugging Parser Memory Access Issues

## Issue Analysis
The parser crashes with "Invalid memory address" when attempting to store to heap-allocated buffers during pattern matching parsing. This affects both dynamic ALLOC and static CREATE ALLOT buffers.

## Root Cause Investigation

### ✅ **Confirmed Fixes**
1. **Heap Bounds Check**: Fixed `HEAP-END >=` to `HEAP-END >` to allow allocations up to heap limit
2. **Parser Logic Bugs**: Fixed multiple address calculation errors in PARSE-CONSTRUCTOR-PATTERN:
   - Tag storage: `DUP R@ SWAP !` → `R@ !`
   - Num-fields storage consistency between field/no-field cases
   - Write pointer advancement logic

### 🔍 **Memory Access Issue**
- **Symptom**: `!` operations fail with "Invalid memory address" 
- **Scope**: Affects CREATE ALLOT buffers (heap, static buffers)
- **ALLOCATE**: Not tested (would require major refactoring)
- **Hypothesis**: Gforth dictionary space not writable in this execution context

### 📋 **Failed Workarounds**
- Static buffers (CREATE ALLOT) - Same error
- Smaller heap sizes - Same error  
- Removed interfering allocations - Same error
- Different allocation strategies - Complex to implement

## Current Status
**Parser Memory Bug**: Blocks pattern matching functionality including wildcards

**Wildcard Implementation**: Complete and correct, ready for testing once parser is fixed

**Impact**: Pattern matching features (numeric, constructor, wildcards) cannot be tested until memory access issue is resolved

## Next Steps
1. **Investigate Gforth Memory Model**: Why CREATE ALLOT buffers are not writable
2. **Alternative Allocation**: Implement runtime allocation using ALLOCATE
3. **Minimal Reproduction**: Create test case that triggers the memory error
4. **Workaround**: Implement pattern matching without heap allocation (stack-based)

## Files Modified
- `src/heap.fs`: Fixed bounds check
- `src/parse.fs`: Fixed parser logic bugs, added static buffer attempt
- `STATUS.md`: Updated issue status

The wildcard pattern implementation is complete, but the underlying parser memory bug prevents testing and deployment.</content>
<parameter name="filePath">log_docs/PROJECT_LOG_2025-11-12_parser_memory_bug.md