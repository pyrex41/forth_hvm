\ reduce.fs - WHNF reduction loop

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

    DROP 2DROP 0 EXIT  \ Stuck term
  THEN

  DROP  \ No reduction possible - already a value or stuck
;

\ WHNF reduction loop
: WHNF ( term -- whnf-term )
  BEGIN
    DUP IS-VALUE? 0= WHILE
    INTERACT-STEP
    DUP 0= IF EXIT THEN  \ Stuck term, stop
  REPEAT
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

  \ Test 6: Parse and reduce (.x x *)
  ." Test 6: Parse and reduce (.x x *)... "
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

  \ Reset iteration counter
  0 ITR-COUNT !
;
