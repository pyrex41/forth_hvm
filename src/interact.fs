\ interact.fs - Interaction rules from INTERS.md

\ Core interaction rules
\ Helper: Walk a term and substitute VAR nodes
\ Forward declaration for recursion
DEFER SUBST-WALK

:NONAME ( term var-loc arg-term -- term' )
  >R >R ( term | R: arg-term var-loc )
  DUP GET-TAG

  \ If it's a VAR, check if it matches the binding location
  DUP TAG-VAR = IF
    DROP DUP GET-VAL ( term var-val )
    R@ = IF
      \ This VAR matches the binding - replace with arg
      DROP R> DROP R> ( arg-term )
      EXIT
    ELSE
      \ Different VAR - keep it
      R> R> 2DROP ( term )
      EXIT
    THEN
  THEN

  \ If it's a LAM, recursively substitute in the body
  DUP TAG-LAM = IF
    DROP DUP GET-LAB ( term lab )
    OVER GET-VAL @ ( term lab body-term )
    R@ R> SUBST-WALK ( term lab body-term' )

    \ Allocate new LAM node (1 cell for body)
    1 ALLOC ( term lab body-term' new-loc )
    TUCK ! ( term lab new-loc )
    ROT DROP ( lab new-loc )
    TAG-LAM -ROT PACK-TERM ( term' )
    R> DROP EXIT
  THEN

  \ If it's APP, recursively substitute in both fun and arg
  DUP TAG-APP = IF
    DROP DUP GET-VAL ( term app-loc )
    DUP @ ( term app-loc fun-term )
    R@ R@ SUBST-WALK ( term app-loc fun-term' )
    OVER CELL+ @ ( term app-loc fun-term' arg-term )
    R@ R> SUBST-WALK ( term app-loc fun-term' arg-term' )

    \ Allocate new APP node (2 cells: fun, arg)
    2 ALLOC ( term app-loc fun-term' arg-term' new-loc )
    DUP >R ( term app-loc fun-term' arg-term' new-loc | R: new-loc )
    SWAP OVER CELL+ ! ( term app-loc fun-term' new-loc | R: new-loc )
    ! ( term app-loc | R: new-loc )
    2DROP R> ( new-loc )
    TAG-APP 0 ROT PACK-TERM EXIT
  THEN

  \ If it's SUP, recursively substitute in both branches
  DUP TAG-SUP = IF
    DROP DUP GET-LAB ( term sup-lab )
    OVER GET-VAL ( term sup-lab sup-loc )
    DUP @ ( term sup-lab sup-loc a-term )
    R@ R@ SUBST-WALK ( term sup-lab sup-loc a-term' )
    OVER CELL+ @ ( term sup-lab sup-loc a-term' b-term )
    R@ R> SUBST-WALK ( term sup-lab sup-loc a-term' b-term' )

    \ Allocate new SUP node (2 cells: a, b)
    2 ALLOC >R ( term sup-lab sup-loc a-term' b-term' | R: new-loc )
    OVER R@ ! ( term sup-lab sup-loc a-term' b-term' | R: new-loc )
    R> DUP >R CELL+ ! ( term sup-lab sup-loc a-term' | R: new-loc )
    2DROP DROP ( sup-lab | R: new-loc )
    R> TAG-SUP -ROT PACK-TERM ( sup-term' )
    EXIT
  THEN

  \ If it's DUP, recursively substitute in target and continuation
  DUP TAG-DUP = IF
    DROP DUP GET-LAB ( term dup-lab )
    OVER GET-VAL ( term dup-lab dup-loc )
    DUP @ ( term dup-lab dup-loc target-term )
    R@ R@ SUBST-WALK ( term dup-lab dup-loc target-term' )
    OVER CELL+ @ ( term dup-lab dup-loc target-term' cont-term )
    R@ R> SUBST-WALK ( term dup-lab dup-loc target-term' cont-term' )

    \ Allocate new DUP node (2 cells: target, cont)
    2 ALLOC >R ( term dup-lab dup-loc target-term' cont-term' | R: new-loc )
    OVER R@ ! ( term dup-lab dup-loc target-term' cont-term' | R: new-loc )
    R> DUP >R CELL+ ! ( term dup-lab dup-loc target-term' | R: new-loc )
    2DROP DROP ( dup-lab | R: new-loc )
    R> TAG-DUP -ROT PACK-TERM ( dup-term' )
    EXIT
  THEN

  \ For ERA, U32, CTR - keep them as-is (no children to substitute)
  DROP R> R> 2DROP ( term )
; IS SUBST-WALK

: APP-LAM ( app-term -- reduced-term )
  \ Beta reduction: (λx.body arg) -> body[x:=arg]
  \ APP term has: fun-ptr at val, arg-ptr at val+CELL
  DUP GET-VAL ( app-term app-loc )
  DUP @ ( app-term app-loc fun-term )
  SWAP CELL+ @ ( app-term fun-term arg-term )

  \ fun-term is LAM, extract var-loc and body
  OVER GET-LAB ( app-term fun-term arg-term var-loc )
  ROT GET-VAL @ ( app-term arg-term body-term var-loc )
  SWAP ( app-term arg-term var-loc body-term )

  \ Now substitute: body[var-loc := arg]
  ROT SWAP ( app-term body-term arg-term var-loc )
  SWAP ( app-term body-term var-loc arg-term )
  SUBST-WALK ( app-term body-term' )

  NIP ( body-term' )
;

: DUP-ERA ( dup-term -- reduced-term )
  \ ! &L{r,s} = *; K -> K[r:=*,s:=*]
  \ DUP term structure: val points to [dup-target, continuation]
  DUP GET-VAL ( dup-term dup-loc )
  CELL+ @ ( dup-term cont-term )

  \ For now, just return continuation
  \ TODO: Properly substitute r and s with ERA in continuation
  NIP ( cont-term )
;

: DUP-SUP ( dup-term -- reduced-term )
  \ ! &L{x,y} = &L{a,b}; K -> x <- a, y <- b, K (same label)
  \ ! &L{x,y} = &R{a,b}; K -> distribute (different labels)

  DUP GET-LAB >R ( dup-term | R: L )
  DUP GET-VAL ( dup-term dup-loc )
  DUP @ ( dup-term dup-loc sup-term )
  SWAP CELL+ @ ( dup-term sup-term cont-term )

  \ Get sup label
  OVER GET-LAB ( dup-term sup-term cont-term R )
  R@ = IF
    \ Case 1: Equal labels (annihilation)
    \ x <- a, y <- b, return K
    SWAP GET-VAL ( dup-term cont-term sup-loc )
    DUP @ SWAP CELL+ @ ( dup-term cont-term a-term b-term )

    \ For now, just return continuation
    \ TODO: Properly bind x and y to a and b in continuation
    2DROP DROP R> DROP ( cont-term )
  ELSE
    \ Case 2: Different labels (distribution)
    \ ! &L{x,y} = &R{a,b}; K
    \ -> x <- &R{a0,b0}, y <- &R{a1,b1}
    \    ! &L{a0,a1} = a; ! &L{b0,b1} = b; K

    \ Get a and b from SUP
    OVER GET-VAL ( dup-term sup-term cont-term sup-loc | R: L )
    DUP @ ( dup-term sup-term cont-term sup-loc a-term | R: L )
    SWAP CELL+ @ ( dup-term sup-term cont-term a-term b-term | R: L )

    \ Get R label from sup-term
    3 PICK GET-LAB >R ( dup-term sup-term cont-term a-term b-term | R: L R )

    \ Create fresh VARs for a0, a1, b0, b1
    4 ALLOC DUP >R ( ... | R: L R vars-loc )

    \ Create a0 VAR
    R@ TAG-VAR 0 ROT PACK-TERM ( dup-term sup-term cont-term a-term b-term a0-var | R: L R vars-loc )

    \ Create a1 VAR
    R@ CELL+ TAG-VAR 0 ROT PACK-TERM ( dup-term sup-term cont-term a-term b-term a0-var a1-var | R: L R vars-loc )

    \ Create b0 VAR
    R@ 2 CELLS + TAG-VAR 0 ROT PACK-TERM ( dup-term sup-term cont-term a-term b-term a0-var a1-var b0-var | R: L R vars-loc )

    \ Create b1 VAR
    R@ 3 CELLS + TAG-VAR 0 ROT PACK-TERM ( dup-term sup-term cont-term a-term b-term a0-var a1-var b0-var b1-var | R: L R vars-loc )

    \ Create &R{a0, b0}
    2 ALLOC DUP >R ( ... | R: L R vars-loc sup0-loc )
    3 PICK OVER ! ( store a0-var )
    OVER R@ CELL+ ! ( store b0-var )
    R> R@ TAG-SUP -ROT PACK-TERM ( dup-term sup-term cont-term a-term b-term a1-var b1-var sup0-term | R: L R vars-loc )

    \ Create &R{a1, b1}
    2 ALLOC DUP >R ( ... | R: L R vars-loc sup1-loc )
    2 PICK OVER ! ( store a1-var )
    OVER R@ CELL+ ! ( store b1-var )
    R> R> TAG-SUP -ROT PACK-TERM ( dup-term sup-term cont-term a-term b-term sup0-term sup1-term | R: L R )

    \ Create DUP: ! &L{a0,a1} = a
    2 ALLOC DUP >R ( ... | R: L R dup-a-loc )
    5 PICK OVER ! ( store a-term )
    \ Store placeholder continuation (will link to next DUP)
    0 R@ CELL+ ! ( temp )
    R> R@ TAG-DUP -ROT PACK-TERM ( dup-term sup-term cont-term b-term sup0-term sup1-term dup-a | R: L R )

    \ Create DUP: ! &L{b0,b1} = b
    2 ALLOC DUP >R ( ... | R: L R dup-b-loc )
    4 PICK OVER ! ( store b-term )
    \ Link to original continuation
    3 PICK R@ CELL+ ! ( store cont-term )
    R> R> DROP R> TAG-DUP -ROT PACK-TERM ( dup-term sup-term cont-term sup0-term sup1-term dup-a dup-b )

    \ Link dup-a continuation to dup-b
    OVER 5 PICK GET-VAL CELL+ ! ( store dup-b in dup-a continuation )

    \ Create result SUP &L{sup0-term, sup1-term} (x and y bindings)
    2 ALLOC DUP >R ( ... | R: L sup-result-loc )
    4 PICK OVER ! ( store sup0-term )
    3 PICK R@ CELL+ ! ( store sup1-term )
    R> R> TAG-SUP -ROT PACK-TERM ( dup-term sup-term cont-term dup-a dup-b result-sup )

    \ Clean up and return the chained DUP structure
    NIP NIP NIP NIP NIP ( dup-a )
  THEN
;

: DUP-LAM ( dup-term -- reduced-term )
  \ ! &L{r,s} = λx.f; K
  \ -> r <- λx0.f0, s <- λx1.f1, x <- &L{x0,x1}, ! &L{f0,f1} = f; K
  \ Create two lambda copies with fresh bindings and substitute variable with SUP

  \ Extract label, lambda term, and continuation
  DUP GET-LAB >R ( dup-term | R: L )
  DUP GET-VAL ( dup-term dup-loc )
  DUP @ ( dup-term dup-loc lam-term )
  SWAP CELL+ @ ( dup-term lam-term cont-term )

  \ Extract lambda's binding ID and body
  OVER GET-LAB ( dup-term lam-term cont-term old-bind-id )
  ROT GET-VAL @ ( dup-term cont-term old-bind-id body-term )

  \ Create fresh binding IDs for x0 and x1
  FRESH-BIND-ID DUP >R ( dup-term cont-term old-bind-id body-term x0-id | R: L x0-id )
  FRESH-BIND-ID DUP >R ( dup-term cont-term old-bind-id body-term x0-id x1-id | R: L x0-id x1-id )

  \ Create VAR terms for x0 and x1
  DUP TAG-VAR 0 ROT PACK-TERM ( dup-term cont-term old-bind-id body-term x1-id x0-var | R: L x0-id x1-id )
  OVER TAG-VAR 0 ROT PACK-TERM ( dup-term cont-term old-bind-id body-term x1-id x0-var x1-var | R: L x0-id x1-id )

  \ Create SUP &L{x0-var, x1-var}
  2 ALLOC DUP >R ( ... | R: L x0-id x1-id sup-loc )
  2 PICK OVER ! ( store x0-var at sup-loc )
  OVER R@ CELL+ ! ( store x1-var at sup-loc+CELL )
  R> R@ TAG-SUP -ROT PACK-TERM ( dup-term cont-term old-bind-id body-term x1-id sup-term | R: L x0-id x1-id )

  \ Substitute old-bind-id with sup-term in body-term
  ROT >R ( dup-term cont-term body-term x1-id sup-term | R: L x0-id x1-id old-bind-id )
  R> SWAP SUBST-WALK ( dup-term cont-term body-term' x1-id | R: L x0-id x1-id )

  \ Create λx0.body-term'
  OVER 1 ALLOC DUP >R ( ... | R: L x0-id x1-id lam0-loc )
  TUCK ! ( dup-term cont-term x1-id lam0-loc )
  R> R@ TAG-LAM -ROT PACK-TERM ( dup-term cont-term x1-id lam0-term | R: L x0-id x1-id )

  \ Create λx1.body-term' (reuse same body pointer for simplicity)
  1 ALLOC DUP >R ( ... | R: L x0-id x1-id lam1-loc )
  3 PICK @ OVER ! ( store same body )
  R> R> TAG-LAM -ROT PACK-TERM ( dup-term cont-term lam0-term lam1-term | R: L x0-id )

  \ Create SUP &L{lam0-term, lam1-term}
  2 ALLOC DUP >R ( ... | R: L x0-id sup-loc )
  2 PICK OVER ! ( store lam0 )
  OVER R@ CELL+ ! ( store lam1 )
  R> R> DROP R> TAG-SUP -ROT PACK-TERM ( dup-term cont-term result-sup )

  \ Clean up stack and return result-sup
  NIP NIP ( result-sup )
;

: APP-SUP ( app-term -- reduced-term )
  \ (&L{a,b} c) -> ! &L{c0,c1} = c; &L{(a c0),(b c1)}
  \ This creates a duplication of the argument
  DUP GET-VAL ( app-term app-loc )
  DUP @ ( app-term app-loc sup-term )
  SWAP CELL+ @ ( app-term sup-term arg-term )

  \ sup-term is SUP with label L, val points to [a, b]
  OVER GET-LAB >R ( app-term sup-term arg-term | R: L )

  \ Get a and b from SUP
  SWAP DUP GET-VAL ( app-term arg-term sup-term sup-loc )
  DUP @ SWAP CELL+ @ ( app-term arg-term a-term b-term )

  \ Create SUP with fresh VARs for c: &L{c0, c1}
  \ Allocate 2 cells for the VAR bindings
  2 ALLOC DUP >R ( app-term arg-term a-term b-term c-vars-loc | R: L c-vars-loc )

  \ Create c0 and c1 VARs
  R@ TAG-VAR 0 ROT PACK-TERM ( app-term arg-term a-term b-term c0-var )
  R@ CELL+ TAG-VAR 0 ROT PACK-TERM ( app-term arg-term a-term b-term c0-var c1-var )

  \ Create (a c0) application
  2 ALLOC >R ( app-term arg-term a-term b-term c0-var c1-var | R: L c-vars-loc app1-loc )
  3 PICK R@ ! ( ... ) \ Store a
  OVER R> DUP >R CELL+ ! ( app-term arg-term a-term b-term c0-var c1-var | R: L c-vars-loc app1-loc )
  R> TAG-APP 0 ROT PACK-TERM ( app-term arg-term a-term b-term c0-var c1-var app1-term )

  \ Create (b c1) application
  2 ALLOC >R ( app-term arg-term a-term b-term c0-var c1-var app1-term | R: L c-vars-loc app2-loc )
  2 PICK R@ ! ( ... ) \ Store b
  OVER R> DUP >R CELL+ ! ( ... | R: L c-vars-loc app2-loc )
  R> TAG-APP 0 ROT PACK-TERM ( app-term arg-term a-term b-term c0-var c1-var app1-term app2-term )

  \ Create &L{app1, app2}
  2 ALLOC >R ( app-term arg-term a-term b-term c0-var c1-var app1-term app2-term | R: L c-vars-loc sup-loc )
  OVER R@ ! ( ... ) \ Store app1
  R> DUP >R CELL+ ! ( app-term arg-term a-term b-term c0-var c1-var app1-term | R: L c-vars-loc sup-loc )
  R> R@ TAG-SUP -ROT PACK-TERM ( app-term arg-term a-term b-term c0-var c1-var app1-term result-sup | R: L c-vars-loc )

  \ Create DUP: ! &L{c0,c1} = arg; result-sup
  \ DUP structure: [dup-term, cont-term] with label L
  2 ALLOC >R ( app-term arg-term a-term b-term c0-var c1-var app1-term result-sup | R: L c-vars-loc dup-loc )
  \ Store arg-term as duplicated term
  4 PICK R@ ! ( ... )
  \ Store result-sup as continuation
  R> DUP >R CELL+ ! ( app-term arg-term a-term b-term c0-var c1-var app1-term | R: L c-vars-loc dup-loc )

  \ Pack DUP term
  R> R> DROP R> TAG-DUP -ROT PACK-TERM ( app-term arg-term a-term b-term c0-var c1-var app1-term dup-term )

  \ Clean up stack
  NIP NIP NIP NIP NIP NIP NIP NIP
;

\ Perform OP2 arithmetic operation
: OP2-COMPUTE ( opcode lhs rhs -- result )
  \ opcode: 0=ADD, 1=SUB, 2=MUL, 3=DIV, 4=MOD, etc.
  ROT ( lhs rhs opcode )
  DUP 0 = IF DROP + EXIT THEN  \ ADD
  DUP 1 = IF DROP SWAP - EXIT THEN  \ SUB
  DUP 2 = IF DROP * EXIT THEN  \ MUL
  DUP 3 = IF DROP SWAP / EXIT THEN  \ DIV
  DUP 4 = IF DROP SWAP MOD EXIT THEN  \ MOD
  DUP 5 = IF DROP AND EXIT THEN  \ AND
  DUP 6 = IF DROP OR EXIT THEN  \ OR
  DUP 7 = IF DROP XOR EXIT THEN  \ XOR
  DUP 8 = IF DROP LSHIFT EXIT THEN  \ SHL
  DUP 9 = IF DROP RSHIFT EXIT THEN  \ SHR
  DUP 10 = IF DROP SWAP < IF -1 ELSE 0 THEN EXIT THEN  \ LT
  DUP 11 = IF DROP SWAP > IF -1 ELSE 0 THEN EXIT THEN  \ GT
  DUP 12 = IF DROP SWAP <= IF -1 ELSE 0 THEN EXIT THEN  \ LE
  DUP 13 = IF DROP SWAP >= IF -1 ELSE 0 THEN EXIT THEN  \ GE
  DUP 14 = IF DROP = IF -1 ELSE 0 THEN EXIT THEN  \ EQ
  DUP 15 = IF DROP <> IF -1 ELSE 0 THEN EXIT THEN  \ NE
  DROP 2DROP 0  \ Unknown opcode
;

\ OP2-U32: Reduce OP2 with two U32 operands
: OP2-U32 ( op2-term -- reduced-term )
  \ OP2 term structure: val points to [lhs, rhs], lab=opcode
  DUP GET-LAB >R ( op2-term | R: opcode )
  GET-VAL ( op2-loc | R: opcode )
  DUP @ ( op2-loc lhs-term | R: opcode )
  SWAP CELL+ @ ( lhs-term rhs-term | R: opcode )

  \ Check if both are U32
  OVER GET-TAG TAG-U32 = OVER GET-TAG TAG-U32 = AND IF
    \ Both are U32, compute result
    GET-VAL SWAP GET-VAL ( rhs-val lhs-val | R: opcode )
    R> OP2-COMPUTE ( result )
    TAG-U32 0 ROT PACK-TERM
  ELSE
    \ Not both U32 - cannot reduce yet
    2DROP R> DROP 0
  THEN
;

: ANNIHILATE ( term -- simplified-term )
  \ TODO: annihilation rules
;

\ Test word
: TEST-INTERACT ( -- )
  ." Interact module loaded" CR
;
