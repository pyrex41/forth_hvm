\ collapse.fs - Normalization and pretty-printing

\ Deep normalization (reduce inside lambdas)
: NORMALIZE ( term -- norm-term )
  \ TODO: recursive normalization
;

\ Collapse rules for SUP elimination
: SUP-LAM-COLLAPSE ( term -- term' )
  \ TODO: float superposition outside lambda
;

\ Variable renaming (α-conversion)
: RENAME ( term -- term' )
  \ TODO: assign fresh variable names
;

\ Pretty-printer
: PRINT ( term -- )
  \ TODO: output as λ-calculus string
  DROP
  ." (term)" CR
;

\ Main collapse entry point
: COLLAPSE ( term -- )
  WHNF
  NORMALIZE
  RENAME
  PRINT
;

\ Test word
: TEST-COLLAPSE ( -- )
  ." Collapse module loaded" CR
;
