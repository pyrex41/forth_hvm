\ core.fs - Core primitives: tags, constants, term packing

\ IC Term Tags (from IC.md)
0 CONSTANT TAG-LAM
1 CONSTANT TAG-APP
2 CONSTANT TAG-SUP
3 CONSTANT TAG-DUP
4 CONSTANT TAG-VAR
5 CONSTANT TAG-ERA
6 CONSTANT TAG-CTR
7 CONSTANT TAG-U32
8 CONSTANT TAG-OP2
9 CONSTANT TAG-REF

\ Term packing/unpacking (adapt to CELL size)
\ For now, simple placeholder - will implement bit packing later
: PACK-TERM ( tag lab val -- cell )
  \ TODO: implement bit packing based on CELL size
  \ For 64-bit: tag:5b lab:18b val:40b
  DROP DROP  \ placeholder
;

: UNPACK-TERM ( cell -- tag lab val )
  \ TODO: implement bit unpacking
  0 0 0  \ placeholder
;

\ Utility accessors (placeholders)
: GET-TAG ( cell -- tag ) DROP 0 ;
: GET-LAB ( cell -- lab ) DROP 0 ;
: GET-VAL ( cell -- val ) DROP 0 ;

\ Test word
: TEST-CORE ( -- )
  ." Core module loaded" CR
  ." Tags defined: LAM=" TAG-LAM .
  ." APP=" TAG-APP .
  ." SUP=" TAG-SUP . CR
;
