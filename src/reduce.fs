\ reduce.fs - WHNF reduction loop

\ Iteration counter for stats
VARIABLE ITR-COUNT
0 ITR-COUNT !

\ Check if term is a value (LAM, SUP, CTR)
: IS-VALUE? ( term -- flag )
  \ TODO: check term tag
  DROP 0
;

\ Apply one interaction step
: INTERACT-STEP ( term -- term' )
  \ TODO: dispatch to interaction rules
  \ Increment counter
  1 ITR-COUNT +!
;

\ WHNF reduction loop
: WHNF ( term -- whnf-term )
  \ TODO: loop until value or stuck
  \ BEGIN DUP IS-VALUE? 0= WHILE INTERACT-STEP REPEAT
;

\ Test word
: TEST-REDUCE ( -- )
  ." Reduce module loaded" CR
;
