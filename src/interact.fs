\ interact.fs - Interaction rules from INTERS.md

\ Core interaction rules
\ Helper: Walk a term and substitute VAR nodes
\ Forward declaration for recursion
DEFER SUBST-WALK

:NONAME ( term var-loc arg-term -- term' )
  >R >R ( term | R: arg-term var-loc )
  DUP GET-TAG
  ." [SUBST tag=" DUP . ." ] "

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
    DROP DUP GET-LAB ." [LAM-lab=" DUP . ." ] " ( term lab )
    OVER GET-VAL ." [LAM-val=" DUP . ." ] " @ ( term lab body-term )
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

  \ For ERA, SUP, DUP, U32, CTR - keep them as-is for now
  \ (full implementation would handle SUP and DUP recursively)
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
    \ x <- &R{a0,b0}, y <- &R{a1,b1}
    \ ! &L{a0,a1} = a; ! &L{b0,b1} = b; K

    \ Get a and b from SUP
    OVER GET-VAL ( dup-term sup-term cont-term sup-loc )
    DUP @ SWAP CELL+ @ ( dup-term sup-term cont-term a-term b-term )

    \ For now, stub this complex case
    2DROP 2DROP DROP R> DROP 0
  THEN
;

: DUP-LAM ( dup-term -- reduced-term )
  \ ! &L{r,s} = λx.f; K
  \ -> r <- λx0.f0, s <- λx1.f1, x <- &L{x0,x1}, ! &L{f0,f1} = f; K
  \ This is complex - needs to duplicate the lambda body

  \ For now, just return the continuation
  \ TODO: Implement full DUP-LAM logic
  DUP GET-VAL CELL+ @ ( dup-term cont-term )
  NIP
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

: ANNIHILATE ( term -- simplified-term )
  \ TODO: annihilation rules
;

\ Test word
: TEST-INTERACT ( -- )
  ." Interact module loaded" CR
;
