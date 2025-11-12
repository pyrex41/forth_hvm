# Constructor Patterns in ForthVM

## Overview
Constructor patterns extend the ForthVM with algebraic data type matching, enabling church numeral operations and recursive data processing. Patterns are expressed using the `~` syntax and support both numeric and constructor-based cases.

## Syntax

### Numeric Patterns
Numeric patterns match against U32 values, supporting zero and successor cases for church numeral processing:

```
~n { 0: zero-body, 1+p: succ-body }
```

- `n`: Scrutinee variable (must be bound in current scope)
- `0:`: Zero case - matches when value = 0
- `1+p:`: Successor case - matches when value > 0, binds `p` to value-1
- `zero-body`: Term to evaluate if match is zero
- `succ-body`: Term to evaluate if match is successor, with `p` bound

### Constructor Patterns
Constructor patterns match against tagged constructors with field bindings:

```
~x { #Tag{fields}: body, #Tag2{fields}: body2 }
```

- `#Tag{fields}`: Constructor pattern with field variable bindings
- `fields`: Comma-separated variable names for constructor fields
- `body`: Term to evaluate if constructor matches

## Implementation Details

### Parser Integration (parse.fs)
- **Token Recognition**: `~` triggers PARSE-PATTERN-MATCH dispatcher
- **Numeric Patterns**: Parses `0:` and `1+p:` cases, stores in 4-cell heap structure
  - Heap layout: [scrut-loc, zero-body, succ-bind-id, succ-body]
  - Lab=0 indicates numeric pattern type
- **Constructor Patterns**: Parses `#Tag{field1, field2}: body` cases
  - Stores case array with tag, field bindings, and body
  - Lab=1 indicates constructor pattern type

### Reduction Logic (reduce.fs)
- **MATCH-REDUCE**: Main reduction rule for TAG-MATCH terms
  - Extracts scrutinee from heap[0]
  - Validates U32 type for numeric patterns
  - Selects branch based on value (0 vs >0)
  - Binds successor variable (p = n-1) for successor case
  - Evaluates selected body via NORMALIZE
  - Cleans up bindings with SUBST-CLEAR

### Heap Structure
Numeric MATCH term (lab=0):
```
match-loc[0]: scrut-loc (pointer to scrutinee location)
match-loc[1]: zero-body (term for zero case)
match-loc[2]: succ-bind-id (binding ID for successor variable)
match-loc[3]: succ-body (term for successor case)
```

### Variable Binding
- **Zero Case**: No bindings needed - evaluates zero-body directly
- **Successor Case**: Binds successor variable to U32 (value-1) using SUBST-PUT
- **Affine Safety**: SUBST-CLEAR called after evaluation to prevent leaks
- **Binding Flow**: Parse-time bind-id stored in MATCH structure, runtime SUBST-PUT uses this ID

## Usage Examples

### Zero Test
```
@test_zero = λn. ~n { 0: 42, 1+p: p }
(@test_zero 0)  // Returns 42
```

### Successor Test
```
@test_succ = λn. ~n { 0: 0, 1+p: (+ p 100) }
(@test_succ 1)  // Returns 101
```

### Nested Patterns
```
@add_one = λn. ~n { 0: 1, 1+p: (+ 1 p) }
@double = λn. ~n { 0: 0, 1+p: (+ p p) }
(@double (@add_one 2))  // Returns 4
```

### Church Numeral Operations
```
@is_zero = λn. ~n { 0: 1, 1+p: 0 }
@fact = λn. ~n { 0: 1, 1+p: (* n (@fact p)) }
(@fact 5)  // Returns 120
```

## Integration with Core Runtime

### Reduction Integration
- **INTERACT-STEP**: Dispatches TAG-MATCH to MATCH-REDUCE
- **WHNF Loop**: MATCH-REDUCE integrated as standard interaction rule
- **Normalization**: NORMALIZE-MATCH stub preserves MATCH structure during deep reduction

### Affine Variable Management
- Successor binding active only during branch evaluation
- SUBST-CLEAR ensures bindings don't leak between evaluations
- Variable capture handled by lexical scoping in parser

## Performance Considerations

### Heap Allocation
- MATCH terms allocate 4 cells for numeric patterns
- Constructor patterns allocate variable size based on case count
- GC Safety: MATCH heap marked during evaluation

### Reduction Efficiency
- Zero case: Direct branch evaluation (no allocation)
- Successor case: Single U32 allocation for binding
- Branch selection: Direct integer comparison (0 vs >0)

## Known Limitations

### Current Implementation
- **Constructor Patterns**: Stubbed - parse but don't reduce (lab=1)
- **Multi-field Constructors**: Single-field only (PARSE-CTR simplification)
- **Nested Patterns**: Supported in syntax, reduction untested
- **Affine Use Counting**: SUBST-CLEAR cleanup only, no SUBST-USE tracking

### Future Enhancements
- Full constructor pattern matching (#Tag{fields}: body)
- Multi-field constructor support in PARSE-CTR
- Nested pattern matching optimization
- Pattern matching in lambda bodies (currently head-only)

## Testing Strategy

### Unit Tests (reduce.fs)
- Zero case: ~n { 0: 42, 1+p: p } applied to 0 returns 42
- Successor case: ~n { 0: 0, 1+p: (+ p 100) } applied to 1 returns 101
- Non-U32 scrutinee: Returns stuck term (0)
- Binding cleanup: Verify no memory leaks after evaluation

### Integration Tests
- test_match.hvm: Zero/successor cases with numeral arithmetic
- test_nested_match.hvm: Nested pattern applications (double(add_one(2)))
- Church numeral operations: factorial, addition, multiplication

### Performance Tests
- Simple numeral recursion: 2+3 should match HVM3 interaction count
- Pattern matching overhead: <10% slowdown vs direct U32 operations
- GC interaction: No thrashing during repeated pattern evaluations

## Error Handling

### Parse Errors
- "Expected variable after ~" - missing scrutinee identifier
- "Expected '0' for first case" - invalid zero case syntax
- "Expected binding variable after 1+" - missing successor binding
- "Expected '{' after scrutinee" - missing pattern braces

### Runtime Errors
- Non-U32 scrutinee: MATCH-REDUCE returns 0 (stuck term)
- Unbound variables in patterns: SUBST-GET returns 0, triggers error
- Heap overflow during MATCH allocation: Handled by ALLOC error path

## Debugging Tips

### Common Issues
- **Binding Leaks**: Missing SUBST-CLEAR after successor evaluation
- **Heap Corruption**: Incorrect MATCH heap layout (4 cells for numeric)
- **Stack Underflow**: Wrong R-stack usage in MATCH-REDUCE
- **Wrong Branch**: U32 value comparison (0 vs >0) logic error

### Debug Commands
```
gforth src/fvm.fs -e 'S" test.hvm" LOAD-INPUT PARSE-TERM .TERM bye'  # Parse debug
gforth src/fvm.fs -e 'DEBUG? ON S" test.hvm" LOAD-INPUT PARSE-TERM WHNF bye'  # Reduction trace
gforth src/fvm.fs -e 'S" test.hvm" LOAD-INPUT PARSE-TERM NORMALIZE . bye'  # Full normalization
```

### Memory Layout Verification
```
: DEBUG-MATCH ( match-term -- )
  DUP GET-TAG ." Tag: " . CR
  DUP GET-LAB ." Type: " . CR
  DUP GET-VAL DUP . CR
  @ ." Scrut: " . CR
  CELL+ @ ." Zero: " . CR
  2 CELLS + @ ." Bind: " . CR
  3 CELLS + @ ." Succ: " . CR
  DROP ;
```

**Status**: Numeric patterns fully functional. Constructor patterns parsing only. Multi-field support pending.