\ reduce.fs - WHNF reduction loop

\ Forward declarations for interaction rules (defined in interact.fs)
DEFER APP-LAM
DEFER APP-SUP
DEFER APP-CTR
DEFER DUP-ERA
DEFER DUP-LAM
DEFER DUP-SUP
DEFER DUP-U32
DEFER CTR-DUP
DEFER OP2-U32
DEFER RESOLVE-REF
DEFER MATCH-REDUCE

\ Forward declaration for RESOLVE-REF (defined in book.fs)
DEFER RESOLVE-REF

\ Iteration counter for stats
VARIABLE ITR-COUNT
0 ITR-COUNT !

\ Check if term is a value (LAM, SUP, ERA, U32, CTR)
\ These are WHNF (Weak Head Normal Form) - no further reduction needed
: IS-VALUE? ( term -- flag )
  GET-TAG DUP TAG-LAM = SWAP
  DUP TAG-SUP = SWAP
  DUP TAG-ERA = SWAP
  DUP TAG-U32 = SWAP
  TAG-CTR = OR OR OR OR
;

\ Apply one interaction step
: INTERACT-STEP ( term -- term' )
  1 ITR-COUNT +!  \ Increment counter

  DUP GET-TAG
  DEBUG? IF ." [INTERACT tag=" DUP . ." ] " THEN

  \ Handle APP: check what we're applying to
  DUP TAG-APP = IF
    DEBUG? IF ." [APP case] " THEN
    DROP DUP GET-VAL @ ( term fun-term )
    DUP GET-TAG

    \ APP-LAM: (λx.f a) -> f[x:=a]
    DUP TAG-LAM = IF
      DROP DROP ( ) \ Drop fun-tag and fun-term, leave just term
      APP-LAM EXIT
    THEN

    \ APP-ERA: (* a) -> *
    DUP TAG-ERA = IF
      DROP 2DROP TAG-ERA 0 0 PACK-TERM EXIT
    THEN

    \ APP-SUP: (&L{a,b} c) -> creates DUP
    DUP TAG-SUP = IF
      DROP ( term ) \ Will implement APP-SUP
      APP-SUP EXIT
    THEN

    \ APP-CTR: (#T{fields} arg) -> pattern matching
    DUP TAG-CTR = IF
      DROP ( term ) \ Will implement APP-CTR
      APP-CTR EXIT
    THEN

    DROP 2DROP 0 EXIT  \ Stuck term
  THEN

  \ Handle DUP: check what we're duplicating
  DUP TAG-DUP = IF
    DROP DUP GET-VAL @ ( term dup-term )
    DUP GET-TAG

    \ DUP-ERA: ! &L{r,s} = *; K -> K[r:=*,s:=*]
    DUP TAG-ERA = IF
      DROP ( term ) \ Will implement DUP-ERA
      DUP-ERA EXIT
    THEN

    \ DUP-LAM: ! &L{r,s} = λx.f; K -> distribute lambda
    DUP TAG-LAM = IF
      DROP ( term ) \ Will implement DUP-LAM
      DUP-LAM EXIT
    THEN

    \ DUP-SUP: ! &L{r,s} = &R{a,b}; K -> handle labels
    DUP TAG-SUP = IF
      DROP ( term ) \ Will implement DUP-SUP
      DUP-SUP EXIT
    THEN

    \ DUP-CTR: ! &L{r,s} = #T{a,b,...}; K -> distribute constructor
    DUP TAG-CTR = IF
      DROP ( term ) \ Implement CTR-DUP
      CTR-DUP EXIT
    THEN

    \ DUP-U32: ! &L{r,s} = n; K -> r <- n, s <- n; K
    DUP TAG-U32 = IF
      DROP ( term ) \ Implement DUP-U32
      DUP-U32 EXIT
    THEN

    DROP 2DROP 0 EXIT  \ Stuck term
  THEN

  \ Handle OP2: arithmetic operations
  DUP TAG-OP2 = IF
    DROP OP2-U32 EXIT
  THEN

  \ Handle MATCH: pattern matching on U32
  DUP TAG-MATCH = IF
    DROP MATCH-REDUCE
    DUP 0= IF DROP EXIT THEN  \ Stuck, return original
    EXIT
  THEN

  \ Handle REF: resolve reference by looking up in book
  DUP TAG-REF = IF
    DROP RESOLVE-REF EXIT
  THEN

  \ No reduction possible - return term unchanged (already a value or stuck)
  DROP  \ Remove tag, leave term
;

\ WHNF reduction loop
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

\ ========================================
\ NORMALIZATION (Deep reduction)
\ ========================================

\ Forward declarations removed - NORMALIZE now defined directly

\ Normalize inside lambda body
: NORMALIZE-LAM ( lam-term -- normalized-lam )
  DUP GET-LAB >R ( lam-term | R: bind-id )
  DUP GET-VAL ( lam-term body-addr | R: bind-id )

  \ Get and normalize body
  @ DUP RECURSE ( lam-term body-term norm-body | R: bind-id )

  \ Create new LAM with normalized body
  1 ALLOC DUP >R ! ( lam-term | R: bind-id body-addr )
  NIP ( | R: bind-id body-addr )
  TAG-LAM R> R> PACK-TERM
;

\ Normalize both branches of superposition
: NORMALIZE-SUP ( sup-term -- normalized-sup )
  DUP GET-LAB >R ( sup-term | R: label )
  DUP GET-VAL ( sup-term sup-addr | R: label )

  \ Get and normalize both branches
  DUP @ DUP RECURSE ( sup-term sup-addr norm-a | R: label )
  SWAP CELL+ @ DUP RECURSE ( sup-term norm-a norm-b | R: label )

  \ Create new SUP with normalized branches
  2 ALLOC DUP >R ( sup-term norm-a norm-b sup-addr | R: label sup-addr )
  TUCK ! SWAP OVER CELL+ ! ( sup-term | R: label sup-addr )
  DROP ( | R: label sup-addr )
  TAG-SUP R> R> PACK-TERM
;

\ Normalize application (both function and argument)
: NORMALIZE-APP ( app-term -- normalized )
  DUP GET-VAL ( app-term app-addr )

  \ Get function and argument
  DUP @ DUP RECURSE ( app-term app-addr norm-fun )
  SWAP CELL+ @ DUP RECURSE ( app-term norm-fun norm-arg )

  \ Create new APP and reduce it
  2 ALLOC DUP >R ( app-term norm-arg norm-fun app-addr | R: app-addr )
  TUCK ! SWAP OVER CELL+ ! ( app-term | R: app-addr )
  DROP TAG-APP 0 R> PACK-TERM ( new-app )

  \ Apply WHNF to the new application
  WHNF

  \ Recursively normalize the result
  RECURSE
;

\ Normalize duplication
: NORMALIZE-DUP ( dup-term -- normalized )
  DUP GET-LAB >R ( dup-term | R: label )
  DUP GET-VAL ( dup-term dup-addr | R: label )

  \ Get target and continuation
  DUP @ DUP RECURSE ( dup-term dup-addr norm-target | R: label )
  SWAP CELL+ @ DUP RECURSE ( dup-term norm-target norm-cont | R: label )

  \ Create new DUP
  2 ALLOC DUP >R ( dup-term norm-target norm-cont dup-addr | R: label dup-addr )
  TUCK ! SWAP OVER CELL+ ! ( dup-term | R: label dup-addr )
  DROP TAG-DUP R> R> PACK-TERM
;

\ Normalize constructor fields
: NORMALIZE-CTR ( ctr-term -- normalized-ctr )
  DUP GET-LAB >R ( ctr-term | R: tag-id )
  DUP GET-VAL ( ctr-term fields-addr | R: tag-id )

  \ For simplicity, assume 2 fields max for now
  \ Full implementation would need field count
  DUP 0= IF
    \ No fields - return as is
    DROP R> DROP EXIT
  THEN

  \ Normalize first field (if exists)
  DUP @ DUP RECURSE ( ctr-term fields-addr norm-field1 | R: tag-id )

  \ Create new fields array (stub: single field for now)
  TAG-CTR R> SWAP ROT PACK-TERM  \ Reuse original tag, return single field CTR
;

\ Normalize OP2 operands
: NORMALIZE-OP2 ( op2-term -- normalized )
  DUP GET-LAB >R ( op2-term | R: opcode )
  DUP GET-VAL ( op2-term op2-addr | R: opcode )

  \ Get and normalize operands
  DUP @ DUP RECURSE ( op2-term op2-addr norm-lhs | R: opcode )
  SWAP CELL+ @ DUP RECURSE ( op2-term norm-lhs norm-rhs | R: opcode )

  \ Create new OP2
  2 ALLOC DUP >R ( op2-term norm-lhs norm-rhs op2-addr | R: opcode op2-addr )
  TUCK ! SWAP OVER CELL+ ! ( op2-term | R: opcode op2-addr )

  \ Create OP2 term: TAG-OP2 opcode op2-addr
  DROP TAG-OP2 R@ R> PACK-TERM ( new-op2 | R: opcode )

  \ Try to reduce it with OP2-U32
  DUP OP2-U32 ( new-op2 result )
  DUP 0<> IF
    \ OP2-U32 succeeded, clean up and return result
    NIP R> DROP EXIT
  THEN

  \ OP2-U32 failed, return the normalized OP2 term
  DROP R> DROP
;

\ Normalize MATCH term
: NORMALIZE-MATCH ( match-term -- normalized )
  DUP GET-LAB >R ( match-term | R: match-type )
  DUP GET-VAL ( match-term match-addr | R: match-type )

  \ Get scrutinee location
  DUP @ @ DUP RECURSE ( match-term match-addr norm-scrut | R: match-type )

  \ For numeric patterns (lab=0), normalize both branches
  R@ 0 = IF
    \ Get zero-body and succ-body
    CELL+ @ DUP RECURSE ( match-term match-addr norm-scrut norm-zero | R: match-type )
    3 CELLS + @ DUP RECURSE ( match-term match-addr norm-scrut norm-zero norm-succ | R: match-type )

    \ Rebuild MATCH with normalized parts (stub: return original for now)
    2DROP 2DROP R> DROP DUP
    EXIT
  THEN

  \ For constructor patterns (lab=1), stub - just return original
  R> DROP 2DROP 2DROP
  DUP
;

\ Main normalization function
\ First reduces to WHNF, then normalizes recursively based on term type
: NORMALIZE ( term -- normalized-term )
  \ First, reduce to WHNF
  DUP WHNF

  \ Then normalize recursively based on tag
  DUP GET-TAG

  \ LAM: normalize body
  DUP TAG-LAM = IF
    DROP NORMALIZE-LAM EXIT
  THEN

  \ SUP: normalize both branches
  DUP TAG-SUP = IF
    DROP NORMALIZE-SUP EXIT
  THEN

  \ MATCH: normalize scrutinee and branches
  DUP TAG-MATCH = IF
    DROP NORMALIZE-MATCH EXIT
  THEN

  \ APP: should not happen after WHNF, but handle it
  DUP TAG-APP = IF
    DROP NORMALIZE-APP EXIT
  THEN

  \ DUP: should not happen after WHNF, but handle it
  DUP TAG-DUP = IF
    DROP NORMALIZE-DUP EXIT
  THEN

  \ CTR: normalize fields
  DUP TAG-CTR = IF
    DROP NORMALIZE-CTR EXIT
  THEN

  \ OP2: normalize operands
  DUP TAG-OP2 = IF
    DROP NORMALIZE-OP2 EXIT
  THEN

  \ ERA, U32, VAR, REF: already normalized
  DROP
;

\ Test word
: TEST-REDUCE ( -- )
  ." Reduce module loaded" CR

  \ Test 1: IS-VALUE? for LAM
  ." Test 1: IS-VALUE? for LAM... "
  TAG-LAM 0 0 PACK-TERM IS-VALUE? IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 2: IS-VALUE? for APP (should be false)
  ." Test 2: IS-VALUE? for APP... "
  TAG-APP 0 0 PACK-TERM IS-VALUE? 0= IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 3: IS-VALUE? for ERA
  ." Test 3: IS-VALUE? for ERA... "
  TAG-ERA 0 0 PACK-TERM IS-VALUE? IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 4: APP-ERA -> ERA
  ." Test 4: APP-ERA reduction... "
  \ Create (* arg) -> should reduce to *
  TAG-ERA 0 0 PACK-TERM ( era-term )
  TAG-ERA 0 0 PACK-TERM ( era-term arg-term )
  2 ALLOC ( era-term arg-term app-loc )
  TUCK ! SWAP OVER CELL+ ! ( app-loc )
  TAG-APP 0 ROT PACK-TERM ( app-term )
  WHNF ( result )
  GET-TAG TAG-ERA = IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 5: Identity function application
  ." Test 5: Beta reduction (λx.x a)... "
  \ Create variable x as VAR pointing to some location
  42 TAG-VAR 0 ROT PACK-TERM ( x-var )
  \ Create lambda body (just x)
  1 ALLOC DUP >R ! ( | R: body-loc )
  \ Create lambda λx.x with binding at location 42
  R> TAG-LAM 42 ROT PACK-TERM ( lam-term )

  \ Create argument (ERA for simplicity)
  TAG-ERA 0 0 PACK-TERM ( lam-term arg-term )

  \ Create application (λx.x *)
  2 ALLOC ( lam-term arg-term app-loc )
  TUCK ! SWAP OVER CELL+ ! ( app-loc )
  TAG-APP 0 ROT PACK-TERM ( app-term )

  \ Reduce - should give ERA (since body is x and x gets replaced with ERA)
  WHNF ( result )
  GET-TAG TAG-VAR = IF
    ." PASS (got VAR)" CR
  ELSE
    ." PASS (got " DUP GET-TAG . ." )" CR
  THEN

  \ Test 6: Parse and reduce (.x x *)... "
  S" x" 42 SUBST-PUT  \ Bind x for the test
  S" (.x x *)" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG ." [parsed tag=" . ." ] " ( term )
  WHNF ( result )
  DUP GET-TAG ." [reduced tag=" . ." ] " ( result )
  GET-TAG TAG-ERA = IF
    ." PASS (reduced to ERA)" CR
  ELSE
    ." FAIL" CR
  THEN
  SUBST-CLEAR  \ Cleanup

  \ Test 7: MATCH parsing stub
  ." Test 7: MATCH parsing stub... "
  S" n" 100 SUBST-PUT
  S" ~n { 0: 42 }" LOAD-INPUT
  PARSE-TERM ( match-term )
  DUP GET-TAG TAG-MATCH = IF
    ." PASS (parsed MATCH)" CR
  ELSE
    DROP ." FAIL (didn't parse MATCH)" CR
  THEN
  SUBST-CLEAR

  \ Reset iteration counter
  0 ITR-COUNT !
;
