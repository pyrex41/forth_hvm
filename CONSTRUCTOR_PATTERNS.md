# Constructor Pattern Implementation

## Overview
Extend pattern matching to support constructor patterns like:
```haskell
~xs {
  #Nil: expr
  #Cons{head tail}: @sum(tail (+ head r))
}
```

## Design

### Pattern Types
- **Type 0: Numeric patterns** (0:, 1+p:) - Already implemented
- **Type 1: Constructor patterns** (#Tag{...}:) - To implement

### MATCH Term Extended Format

**Label field encoding:**
- Bits 0-15: Pattern type (0=numeric, 1=constructor, 2=mixed)
- Bits 16-17: Number of cases (for constructor patterns)

**Heap layout for constructor patterns:**
```
match-loc[0] = scrut-loc (variable location to match on)
match-loc[1] = num-cases (number of constructor cases)
match-loc[2] = case-array-ptr (pointer to case definitions)

case-array-ptr points to:
[tag1, num-fields1, bind-id1, bind-id2, ..., body1,
 tag2, num-fields2, bind-id1, bind-id2, ..., body2,
 ...]
```

### Parsing Strategy

1. **Detect pattern type** by looking at first case:
   - If starts with digit (0, 1): numeric pattern
   - If starts with # (#Tag): constructor pattern

2. **Parse constructor case:**
   ```
   #Tag : body           // Nullary constructor (0 fields)
   #Tag{f1 f2 ...} : body  // N-ary constructor (N fields)
   ```

3. **For each field binding:**
   - Get fresh BIND-ID
   - Add to SUBST map: field-name -> bind-id
   - Store bind-id in case array

### Reduction Strategy

**MATCH-REDUCE-CONSTRUCTOR:**
```
1. Evaluate scrutinee to WHNF
2. Check if scrutinee is CTR
3. Get scrutinee tag
4. Iterate through cases to find matching tag
5. If match found:
   a. Extract constructor fields
   b. Bind fields to variables using SUBST-WALK
   c. Return body with substitutions
6. If no match: return ERA (or error)
```

### Example Execution

**Input:**
```haskell
@sum = .xs .r ~xs {
  #Nil: r
  #Cons{head tail}: @sum(tail (+ head r))
}
@main = @sum(#Cons{1 #Cons{2 #Nil}} 0)
```

**Execution trace:**
1. `@sum(#Cons{1 #Cons{2 #Nil}} 0)` - Apply sum
2. Scrutinee xs = `#Cons{1 #Cons{2 #Nil}}`
3. Match against cases:
   - `#Nil`? No, tag doesn't match
   - `#Cons{head tail}`? Yes!
4. Extract fields: head=1, tail=#Cons{2 #Nil}
5. Return: `@sum(#Cons{2 #Nil} (+ 1 0))`
6. Reduce (+ 1 0) = 1
7. Continue: `@sum(#Cons{2 #Nil} 1)`
8. ... eventually get 3

## Implementation Plan

### Phase 1: Parser Extensions (~300 LOC)
- [ ] Detect pattern type in PARSE-PATTERN-MATCH
- [ ] Implement PARSE-CONSTRUCTOR-CASE
- [ ] Parse field bindings and store bind-ids
- [ ] Build case array in heap

### Phase 2: Reduction (~200 LOC)
- [ ] Implement MATCH-REDUCE-CONSTRUCTOR
- [ ] Field extraction from CTR terms
- [ ] Multiple substitutions for field bindings
- [ ] Integration with existing MATCH-REDUCE

### Phase 3: Testing (~100 LOC)
- [ ] Simple constructor pattern tests
- [ ] bench_sum_range.hvm (List with #Nil/#Cons)
- [ ] enum_nat.hvm (Nat with #Z/#S)

## Complexity Estimate
- Parser: ~300 LOC
- Reduction: ~200 LOC
- Tests: ~100 LOC
- **Total: ~600 LOC**
