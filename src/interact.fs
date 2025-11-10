\ interact.fs - Interaction rules from INTERS.md

\ Core interaction rules (stubs)
: APP-LAM ( app-term -- reduced-term )
  \ TODO: beta reduction (@(λx.body) arg) -> body[x:=arg]
;

: DUP-SUP ( dup-term -- reduced-term )
  \ TODO: superposition distribution
;

: DUP-LAM ( dup-term -- reduced-term )
  \ TODO: lambda duplication
;

: APP-SUP ( app-term -- reduced-term )
  \ TODO: application to superposition
;

: ANNIHILATE ( term -- simplified-term )
  \ TODO: annihilation rules
;

\ Test word
: TEST-INTERACT ( -- )
  ." Interact module loaded" CR
;
