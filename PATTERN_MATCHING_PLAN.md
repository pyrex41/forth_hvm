# Pattern Matching Implementation Plan

## Pattern Syntax Analysis

### From HVM3 Examples

**Numeric Patterns:**
```haskell
~n {
  0: expr           // Zero case
  1+p: expr         // Successor case (p = n-1)
  _: expr           // Wildcard (catch-all)
}
```

**Constructor Patterns:**
```haskell
~xs {
  #Nil: expr                    // Nullary constructor
  #Cons{head tail}: expr        // N-ary constructor with field bindings
}
```

**Nested Patterns:**
```haskell
~a !b {
  #Z: b
  #S{ap}: ~b {              // Nested match inside case
    #Z: 0
    #S{bp}: @eq(ap bp)
  }
}
```

**With Strict Evaluation:**
```haskell
@sum(!xs r) = ~xs !r {        // !xs forces evaluation before matching
  #Nil: r
  #Cons{head tail}: @sum(tail (+ head r))
}
```

**With Embedded DUPs:**
```haskell
1+p: !&0{p0 p1}=p @range(p0 #Cons{p1 xs})
```

## Desugaring Strategy

### Numeric Pattern Desugaring

**Input:**
```haskell
~n {
  0: zero_expr
  1+p: succ_expr
}
```

**Desugar to Core IC:**
```haskell
// Check if n == 0
!&L{n0, n1} = n;
!&M{cond0, cond1} = (== n0 0);
(cond0
  zero_expr
  [p := (- n1 1)] succ_expr)
```

**Key insight:** Use SUP to represent the conditional branches!
- If `n == 0` is true (returns 1), we want first branch
- If false (returns 0), we want second branch
- SUP `&0{branch_zero, branch_succ}` lets us select

Actually, a better encoding:
```haskell
// Direct conditional using SUP
~n { 0: a, 1+p: b }

// Becomes:
(== n 0)
  a                    // if true
  [p := (- n 1)] b     // if false
```

But HVM doesn't have built-in if/then/else. We need to use SUP cleverly.

Actually, looking at the @if example from enum_nat.hvm:
```haskell
@if(b t f) = ~b {
  0: f
  _: t
}
```

This suggests pattern matching on boolean is primitive. So numeric patterns might be:

```haskell
~n { 0: a, 1+p: b }

// Desugar to a special "numeric pattern match" primitive
// OR desugar to multiple checks:

!&L{n_check, n_val} = n;
((== n_check 0)
  a
  [p := (- n_val 1)] b)
```

### Constructor Pattern Desugaring

**Input:**
```haskell
~xs {
  #Nil: nil_expr
  #Cons{h t}: cons_expr
}
```

**Desugar to:**
```haskell
// Check constructor tag
// Extract fields if constructor matches
// Return appropriate branch

!&L{xs_tag_check, xs_fields} = xs;
(check_tag xs_tag_check 'Nil')
  nil_expr
  [h := (field 0 xs_fields), t := (field 1 xs_fields)] cons_expr
```

**Problem:** We don't have a tag checking primitive!

**Solution:** Constructors need runtime tag information. Two approaches:

1. **Boxed constructors**: `#Tag{fields}` becomes `(tag_id, [fields])`
2. **Use label field**: CTR's label already stores tag ID!

Since we store tag in GET-LAB, we can check it:

```haskell
~xs {
  #Nil: a        // tag = 'N' (ASCII 78)
  #Cons{h t}: b  // tag = 'C' (ASCII 67)
}

// Desugar to:
!&L{xs0, xs1} = xs;
(== (GET-TAG xs0) TAG-CTR)
  (== (GET-LAB xs0) 78)  // Nil tag
    a
    [h := field0(xs1), t := field1(xs1)] b
  *  // Not a constructor (error)
```

## Implementation Phases

### Phase 1: Parser Extensions

**New tokens:**
- `~` (tilde) - pattern match operator
- `:` (colon) - case separator
- `1+` - successor pattern prefix
- `_` (underscore) - wildcard pattern
- `data` - ADT declaration keyword

**New parse functions:**
```forth
: PARSE-PATTERN-MATCH ( -- term )
  // ~scrutinee !forced { cases }
;

: PARSE-CASE-BRANCH ( -- pattern-type pattern-data body-term )
  // 0: expr
  // 1+p: expr
  // #Tag: expr
  // #Tag{x y}: expr
  // _: expr
;

: PARSE-DATA-DECL ( -- )
  // data Name { #Cons1 #Cons2{field1} }
  // Store constructor info in a dictionary
;
```

### Phase 2: AST Representation

**New term types:**
```forth
11 CONSTANT TAG-MATCH     // Pattern match node
12 CONSTANT TAG-CASE      // Case branch

// MATCH term structure:
// val: heap address -> [scrutinee, cases-list]
// lab: flags (strict evaluation, etc.)

// CASE term structure:
// val: heap address -> [pattern-type, pattern-data, body]
// lab: pattern type (0=numeric, 1=constructor, 2=wildcard)
```

### Phase 3: Desugaring

**Desugar before reduction:**
```forth
: DESUGAR-MATCH ( match-term -- core-ic-term )
  // Convert MATCH to core IC using DUP/SUP/APP/LAM
;

: DESUGAR-NUMERIC-PATTERN ( scrutinee cases -- term )
  // Convert numeric pattern to equality checks
;

: DESUGAR-CTR-PATTERN ( scrutinee cases -- term )
  // Convert constructor pattern to tag checks + field extraction
;
```

### Phase 4: Integration

Add desugaring pass between parsing and reduction:
```forth
: RUN-FILE ( filename -- )
  LOAD-FILE
  PARSE-ALL
  DESUGAR-ALL     // NEW: desugar patterns to core IC
  REDUCE
  DISPLAY
;
```

## Simplified Initial Approach

**For Phase 1, implement minimal pattern matching:**

1. **Only numeric patterns**: `0:` and `1+p:`
2. **No nested patterns**: Single level only
3. **No constructor patterns**: Just numbers
4. **Direct desugaring**: Expand to core IC immediately

This gets us `bench_count.hvm` working!

### Minimal Numeric Pattern Implementation

**Parse:**
```forth
: PARSE-NUM-MATCH ( -- term )
  // ~n { 0: a, 1+p: b }
  EXPECT-TILDE
  PARSE-VAR ( scrutinee-name )
  EXPECT-LBRACE
  PARSE-CASE-ZERO ( zero-body )
  PARSE-CASE-SUCC ( succ-body )
  EXPECT-RBRACE
  // Immediately desugar to core IC
  CREATE-IF-THEN-ELSE
;
```

**Desugar to:**
```forth
// ~n { 0: a, 1+p: b }
// Becomes:
// ((n == 0) a [p:=n-1]b)

: CREATE-IF-THEN-ELSE ( scrutinee zero-body succ-body -- term )
  // Use a SUP-based conditional
  // if (scrutinee == 0)
  //   then zero-body
  //   else succ-body[p := scrutinee-1]
;
```

## Testing Strategy

**Test 1:** Simple numeric pattern
```haskell
@test = ~n { 0: 1, 1+p: (+ p 1) }
@main = @test(5)
// Expected: 5 (since 5-1+1 = 5)
```

**Test 2:** bench_count simplified
```haskell
@count(n) = ~n { 0: 0, 1+p: (+ 1 @count(p)) }
@main = @count(10)
// Expected: 10
```

**Test 3:** bench_count full
```haskell
@count(!n k) = ~n !k { 0: k, 1+p: @count(p,(+ k 2)) }
@main = @count(10 0)
// Expected: 20
```

## Next Steps

1. ✅ Analyze HVM3 pattern syntax (done above)
2. Add `~` and `:` tokens to lexer
3. Implement PARSE-NUM-MATCH for simple numeric patterns
4. Implement desugaring to core IC
5. Test with simple examples
6. Extend to full numeric patterns with strict eval
7. Add constructor patterns (phase 2)

Let's start coding!
