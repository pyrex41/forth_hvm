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

  \ If it's OP2, recursively substitute in both operands
  DUP TAG-OP2 = IF
    DROP DUP GET-LAB ( term opcode )
    OVER GET-VAL ( term opcode op2-loc )
    DUP @ ( term opcode op2-loc lhs-term )
    R@ R@ SUBST-WALK ( term opcode op2-loc lhs-term' )
    OVER CELL+ @ ( term opcode op2-loc lhs-term' rhs-term )
    R@ R> SUBST-WALK ( term opcode op2-loc lhs-term' rhs-term' )

    \ Allocate new OP2 node (2 cells: lhs, rhs)
    2 ALLOC >R ( term opcode op2-loc lhs-term' rhs-term' | R: new-loc )
    OVER R@ ! ( term opcode op2-loc lhs-term' rhs-term' | R: new-loc )
    R> DUP >R CELL+ ! ( term opcode op2-loc lhs-term' | R: new-loc )
    2DROP DROP ( opcode | R: new-loc )
    R> TAG-OP2 -ROT PACK-TERM ( op2-term' )
    EXIT
  THEN

  \ If it's CTR, recursively substitute in constructor fields
  DUP TAG-CTR = IF
    DROP DUP GET-LAB ( term ctr-tag )
    OVER GET-VAL ( term ctr-tag fields-loc )
    DUP 0= IF
      \ No fields - return as-is
      2DROP R> R> 2DROP EXIT
    THEN
    DUP @ ( term ctr-tag fields-loc field1-term )
    R@ R@ SUBST-WALK ( term ctr-tag fields-loc field1-term' )
    OVER CELL+ @ ( term ctr-tag fields-loc field1-term' field2-term )
    R@ R> SUBST-WALK ( term ctr-tag fields-loc field1-term' field2-term' )

    \ Allocate new CTR fields (2 cells for now)
    2 ALLOC >R ( term ctr-tag fields-loc field1-term' field2-term' | R: new-loc )
    OVER R@ ! ( term ctr-tag fields-loc field1-term' field2-term' | R: new-loc )
    R> DUP >R CELL+ ! ( term ctr-tag fields-loc field1-term' | R: new-loc )
    2DROP DROP ( ctr-tag | R: new-loc )
    R> TAG-CTR -ROT PACK-TERM ( ctr-term' )
    EXIT
  THEN

  \ If it's MATCH, recursively substitute in scrutinee and case bodies
  DUP TAG-MATCH = IF
    DROP ( term )
    \ MATCH structure: val points to [scrut, cases-array]
    DUP GET-VAL ( term match-loc )
    DUP @ ( term match-loc scrut-term )
    R@ R@ SUBST-WALK ( term match-loc scrut-term' )
    SWAP CELL+ @ ( term scrut-term' cases-ptr )

    \ For simplicity, substitute in case bodies (full implementation would iterate)
    \ For now, keep cases as-is since they need proper handling
    2DROP R> R> 2DROP EXIT
  THEN

  \ For ERA, U32, VAR, REF - keep them as-is (no children to substitute)
  DROP R> R> 2DROP ( term )
; IS SUBST-WALK

:NONAME ( app-term -- reduced-term )
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
; IS APP-LAM

:NONAME ( dup-term -- reduced-term )
  \ ! &L{r,s} = *; K -> K[r:=*,s:=*]
  \ DUP term structure: val points to [dup-target, continuation]
  DUP GET-VAL ( dup-term dup-loc )
  CELL+ @ ( dup-term cont-term )

  \ For now, just return continuation
  \ TODO: Properly substitute r and s with ERA in continuation
  NIP ( cont-term )
; IS DUP-ERA

:NONAME ( dup-term -- reduced-term )
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
; IS DUP-SUP

:NONAME ( dup-term -- reduced-term )
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
; IS DUP-LAM

:NONAME ( app-term -- reduced-term )
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
; IS APP-SUP

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
:NONAME ( op2-term -- reduced-term )
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
; IS OP2-U32

: ANNIHILATE ( term -- simplified-term )
  \ TODO: annihilation rules
;

\ CTR-DUP: ! &L{r,s} = #T{a1,a2,...,aN}; K
\ -> K[r := #T{r1,r2,...,rN}, s := #T{s1,s2,...,sN}]
\ where each field ai is duplicated with ! &L{ri,si} = ai
:NONAME ( dup-term -- reduced-term )
  \ DUP structure: [target, cont], where target is CTR
  DUP GET-LAB >R ( dup-term | R: L )
  DUP GET-VAL ( dup-term dup-loc | R: L )

  \ Get target (CTR) and continuation
  DUP @ ( dup-term dup-loc ctr-term | R: L )
  SWAP CELL+ @ ( dup-term ctr-term cont-term | R: L )
  SWAP ( dup-term cont-term ctr-term | R: L )

  \ Get CTR tag and fields
  DUP GET-LAB ( dup-term cont-term ctr-term tag-id | R: L )
  SWAP GET-VAL ( dup-term cont-term tag-id fields-addr | R: L )

  \ Calculate field count (need to determine arity)
  \ For simplicity, we'll read fields until we hit ERA or end
  \ Actually, we stored field count during parsing - but we didn't!
  \ Let me assume a maximum of 4 fields for now as a simplification

  \ TODO: Store field count in CTR during parsing
  \ For now, hardcode 2 fields as a demo
  2 >R ( dup-term cont-term tag-id fields-addr | R: L N )

  \ Create fresh VAR pairs for each field (r1,s1), (r2,s2), ...
  \ Allocate space for N*2 VARs
  R@ 2* ALLOC ( dup-term cont-term tag-id fields-addr vars-base | R: L N )
  DUP >R ( dup-term cont-term tag-id fields-addr vars-base | R: L N vars-base )

  \ Create VAR terms for each duplicate pair
  R@ 2 CELLS + ( dup-term cont-term tag-id fields-addr vars-base v1-loc | R: L N vars-base )
  TAG-VAR 0 ROT PACK-TERM ( dup-term cont-term tag-id fields-addr vars-base r1-var | R: L N vars-base )
  R@ 3 CELLS + ( ... s1-loc | R: L N vars-base )
  TAG-VAR 0 ROT PACK-TERM ( dup-term cont-term tag-id fields-addr vars-base r1-var s1-var | R: L N vars-base )

  \ Create CTR copies: #T{r1,r2,...} and #T{s1,s2,...}
  \ Allocate fields for first copy
  R> DROP R@ ALLOC ( dup-term cont-term tag-id fields-addr r1-var s1-var r-fields | R: L N )
  DUP >R OVER ROT CELL+ @ SWAP ! ( dup-term cont-term tag-id fields-addr r1-var s1-var | R: L N r-fields )

  \ Pack first CTR copy
  R> 3 PICK TAG-CTR -ROT PACK-TERM ( dup-term cont-term tag-id fields-addr r1-var s1-var r-ctr | R: L N )

  \ Allocate fields for second copy
  2 ALLOC ( dup-term cont-term tag-id fields-addr r1-var s1-var r-ctr s-fields | R: L N )
  DUP >R 3 PICK ROT CELL+ @ SWAP ! ( dup-term cont-term tag-id fields-addr r1-var r-ctr | R: L N s-fields )

  \ Pack second CTR copy
  R> 4 PICK TAG-CTR -ROT PACK-TERM ( dup-term cont-term tag-id fields-addr r1-var r-ctr s-ctr | R: L N )

  \ For demo, just return a simple reduction to the first copy
  \ Full implementation would create nested DUPs and substitute
  NIP NIP NIP NIP NIP NIP
  R> DROP R> DROP
; IS CTR-DUP

\ MATCH-REDUCE-CONSTRUCTOR: Pattern matching on constructors
\ ~xs { #Nil: e1, #Cons{h t}: e2 }
\ Heap layout: [scrut-loc, num-cases, case-array-ptr]
\ Case array: [tag1, num-fields1, bind-id1, ..., body1, tag2, ...]
: MATCH-REDUCE-CONSTRUCTOR ( match-term -- reduced-term )
  DUP GET-VAL ( match-term match-loc )

  \ Load match data
  DUP @ ( match-term match-loc scrut-loc )
  OVER CELL+ @ ( match-term match-loc scrut-loc num-cases )
  2 PICK 2 CELLS + @ ( match-term match-loc scrut-loc num-cases case-array-ptr )

  \ Clean up and prepare
  >R >R ( match-term match-loc scrut-loc | R: case-array-ptr num-cases )
  NIP ( scrut-loc | R: case-array-ptr num-cases )

  \ Evaluate scrutinee to WHNF
  TAG-VAR 0 ROT PACK-TERM ( scrut-var | R: case-array-ptr num-cases )
  WHNF ( scrut-whnf | R: case-array-ptr num-cases )

  \ Check if it's a CTR
  DUP GET-TAG TAG-CTR <> IF
    \ Not a constructor - return ERA
    DROP R> R> 2DROP
    TAG-ERA 0 0 PACK-TERM EXIT
  THEN

  \ Get constructor tag and fields
  DUP GET-LAB ( scrut-whnf scrut-tag | R: case-array-ptr num-cases )
  SWAP GET-VAL ( scrut-tag scrut-fields-ptr | R: case-array-ptr num-cases )

  \ Iterate through cases to find matching tag
  R> R> ( scrut-tag scrut-fields-ptr num-cases case-array-ptr )
  SWAP 0 ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx )

  BEGIN
    \ Check if we've exhausted all cases
    DUP 3 PICK >= IF
      \ No match found - return ERA
      2DROP 2DROP DROP
      TAG-ERA 0 0 PACK-TERM EXIT
    THEN

    \ Get current case pointer
    3 PICK ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr )

    \ Read case tag
    DUP @ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr case-tag )

    \ Check if tags match
    5 PICK = IF
      \ Match found!
      \ Read num-fields and body
      DUP CELL+ @ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields )

      \ Body is at: curr-case-ptr + (2 + num-fields) * CELL
      OVER OVER 2 + CELLS + @ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields body )

      \ Now bind fields to variables
      \ Bind-ids are at: curr-case-ptr + 2*CELL, 3*CELL, ...
      \ Field values are at: scrut-fields-ptr[0], [1], ...

      \ For each field, substitute in body
      SWAP ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields )

      \ Iterate through fields
      0 ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx )
      BEGIN
        DUP 2 PICK < WHILE ( ... body num-fields field-idx )

        \ Get bind-id for this field
        3 PICK ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx curr-case-ptr )
        OVER 2 + CELLS + @ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx bind-id )

        \ Get field value
        8 PICK ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx bind-id scrut-fields-ptr )
        5 PICK CELLS + @ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx bind-id field-value )

        \ Substitute in body
        3 PICK ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx bind-id field-value body )
        -ROT ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx body bind-id field-value )
        SUBST-WALK ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx body' )

        \ Update body
        2 PICK >R ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr body num-fields field-idx body' | R: field-idx )
        2 PICK DROP ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields field-idx body' | R: field-idx )
        R> ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields field-idx body' field-idx )
        ROT ROT ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields body' field-idx )

        \ Increment field-idx
        1+ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields body' field-idx' )
      REPEAT

      \ Clean up and return body
      DROP NIP NIP ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx body' )
      NIP NIP NIP NIP NIP ( body' )
      EXIT
    THEN

    \ Tags don't match - advance to next case
    \ Calculate case size: 2 + num-fields + 1 = 3 + num-fields
    DUP CELL+ @ ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr num-fields )
    3 + CELLS ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx curr-case-ptr case-size )
    + ( scrut-tag scrut-fields-ptr case-array-ptr num-cases case-idx next-case-ptr )

    \ Update case array pointer and index
    2 PICK DROP SWAP ( scrut-tag scrut-fields-ptr next-case-ptr num-cases case-idx )
    1+ ( scrut-tag scrut-fields-ptr next-case-ptr num-cases case-idx' )
    0 \ Continue loop
  0= UNTIL

  \ Should not reach here
  2DROP 2DROP DROP
  TAG-ERA 0 0 PACK-TERM
;

\ MATCH-REDUCE: Pattern matching dispatcher
\ Checks label to determine numeric (0) or constructor (1) pattern
:NONAME ( match-term -- reduced-term )
  \ Check pattern type from label
  DUP GET-LAB ( match-term pattern-type )
  1 = IF
    \ Constructor pattern
    DROP MATCH-REDUCE-CONSTRUCTOR EXIT
  THEN
  DROP ( match-term )

  \ Numeric pattern (original code)
  \ Get match data from heap
  DUP GET-VAL ( match-term match-loc )

  \ Load all 4 values from heap
  DUP @ ( match-term match-loc scrut-loc )
  OVER CELL+ @ ( match-term match-loc scrut-loc zero-body )
  2 PICK 2 CELLS + @ ( match-term match-loc scrut-loc zero-body succ-bind-id )
  3 PICK 3 CELLS + @ ( match-term match-loc scrut-loc zero-body succ-bind-id succ-body )

  \ Clean up match-term and match-loc from stack
  >R >R >R ( match-term match-loc scrut-loc | R: succ-body succ-bind-id zero-body )
  NIP ( scrut-loc | R: succ-body succ-bind-id zero-body )

  \ Look up scrutinee value (it's a VAR location)
  TAG-VAR 0 ROT PACK-TERM ( scrut-var-term | R: succ-body succ-bind-id zero-body )

  \ Reduce scrutinee to WHNF to get actual value
  WHNF ( scrut-whnf | R: succ-body succ-bind-id zero-body )

  \ Check if it's a U32
  DUP GET-TAG TAG-U32 <> IF
    \ Not a U32 - error or stuck
    \ For now, return ERA as placeholder
    DROP R> R> R> 2DROP DROP
    TAG-ERA 0 0 PACK-TERM EXIT
  THEN

  \ Get the U32 value
  DUP GET-VAL ( scrut-whnf n-value | R: succ-body succ-bind-id zero-body )

  \ Check if it's 0
  DUP 0= IF
    \ Zero case: return zero_body
    2DROP ( | R: succ-body succ-bind-id zero-body )
    R> NIP R> R> 2DROP ( zero-body )
    EXIT
  THEN

  \ Non-zero case: compute p = n - 1
  1- ( scrut-whnf p-value | R: succ-body succ-bind-id zero-body )

  \ Create U32 term for p
  TAG-U32 0 ROT PACK-TERM ( scrut-whnf p-term | R: succ-body succ-bind-id zero-body )

  \ Store p in SUBST map using succ-bind-id
  NIP ( p-term | R: succ-body succ-bind-id zero-body )
  R> DROP ( p-term | R: succ-body zero-body )
  R> ( p-term succ-bind-id | R: succ-body )
  SWAP ( succ-bind-id p-term | R: succ-body )

  \ We need to bind p to the SUBST map
  \ But we only have the bind-id, not the variable name
  \ For now, let's substitute directly in succ_body using SUBST-WALK
  R> ( succ-bind-id p-term succ-body )
  -ROT ( succ-body succ-bind-id p-term )
  SUBST-WALK ( succ-body' )
; IS MATCH-REDUCE

\ Test word
: TEST-INTERACT ( -- )
  ." Interact module loaded" CR
;
