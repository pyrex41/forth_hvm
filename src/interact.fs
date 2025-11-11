\ interact.fs - Interaction rules from INTERS.md

\ Core interaction rules
: APP-LAM ( app-term -- reduced-term )
  \ Beta reduction: (λx.body arg) -> body[x:=arg]
  \ APP term has: fun-ptr at val, arg-ptr at val+CELL
  DUP GET-VAL ( app-term app-loc )
  DUP @ ( app-term app-loc fun-term )
  SWAP CELL+ @ ( app-term fun-term arg-term )

  \ fun-term is LAM, its val points to body
  SWAP GET-VAL @ ( app-term arg-term body-term )

  \ For now, just return body (TODO: proper substitution)
  \ In a real implementation, we'd walk body-term and replace
  \ VAR nodes that match the lambda's binding location
  \ with the arg-term

  \ Simplified: just return body for now
  NIP ( body-term )

  \ TODO: Implement proper substitution using SUBST-* functions
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
  \ TODO: superposition distribution
  \ ! &L{x,y} = &L{a,b}; K -> x <- a, y <- b, K (same label)
  \ ! &L{x,y} = &R{a,b}; K -> distribute (different labels)
  DROP 0  \ Stub for now
;

: DUP-LAM ( dup-term -- reduced-term )
  \ ! &L{r,s} = λx.f; K
  \ -> r <- λx0.f0, s <- λx1.f1, x <- &L{x0,x1}, ! &L{f0,f1} = f; K
  \ This is complex - needs to duplicate the lambda body
  DROP 0  \ Stub for now
;

: APP-SUP ( app-term -- reduced-term )
  \ (&L{a,b} c) -> ! &L{c0,c1} = c; &L{(a c0),(b c1)}
  \ This creates a duplication of the argument
  DROP 0  \ Stub for now
;

: ANNIHILATE ( term -- simplified-term )
  \ TODO: annihilation rules
;

\ Test word
: TEST-INTERACT ( -- )
  ." Interact module loaded" CR
;
